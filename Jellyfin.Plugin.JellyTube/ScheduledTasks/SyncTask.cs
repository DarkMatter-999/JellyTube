using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Jellyfin.Plugin.JellyTube.YtDlp;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTube.ScheduledTasks;

/// <summary>
/// Scheduled task that syncs YouTube sources and generates .strm/.nfo files.
/// </summary>
public class SyncTask : IScheduledTask
{
    private readonly ILogger<SyncTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncTask"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public SyncTask(ILogger<SyncTask> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync YouTube Sources";

    /// <inheritdoc />
    public string Key => "YouTubePluginSync";

    /// <inheritdoc />
    public string Description => "Fetches latest videos from added YouTube sources";

    /// <inheritdoc />
    public string Category => "YouTube";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        var db = Plugin.Instance?.DbContext;

        if (config == null || db == null || config.SourceUrls == null || config.SourceUrls.Count == 0)
        {
            return;
        }

        var wrapper = new YtDlpWrapper(config.YtDlpPath, _logger);
        using var connection = db.GetConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        int count = 0;
        foreach (var url in config.SourceUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Syncing YouTube source: {Url}", url);

            // --- Fetch full source metadata (title, description, thumbnail) ---
            var sourceInfo = await wrapper.GetSourceMetadataAsync(url, cancellationToken).ConfigureAwait(false);

            // --- Fetch playlist items ---
            var (items, flatSourceInfo) = await wrapper.GetPlaylistItemsAsync(url, cancellationToken).ConfigureAwait(false);

            // Use detailed source info if available, otherwise fallback to flat-playlist data
            sourceInfo ??= flatSourceInfo;

            // --- Upsert Source ---
            var existingSource = await connection.QueryFirstOrDefaultAsync<Data.Models.Source>(
                "SELECT * FROM DMJT_Sources WHERE Url = @Url", new { Url = url }).ConfigureAwait(false);

            var sourceId = existingSource?.Id ?? Guid.NewGuid().ToString();
            var sourceTitle = sourceInfo?.Title ?? existingSource?.Title ?? url;
            var sourceThumbnail = sourceInfo?.ThumbnailUrl ?? existingSource?.ThumbnailUrl ?? string.Empty;

            if (existingSource == null)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO DMJT_Sources (Id, Url, Type, Title, Uploader, Description, ThumbnailUrl, ChannelId, LastSyncedAt)
                      VALUES (@Id, @Url, @Type, @Title, @Uploader, @Description, @ThumbnailUrl, @ChannelId, @LastSyncedAt)",
                    new
                    {
                        Id = sourceId,
                        Url = url,
                        Type = "Playlist/Channel",
                        Title = sourceTitle,
                        Uploader = sourceInfo?.Uploader ?? string.Empty,
                        Description = sourceInfo?.Description ?? string.Empty,
                        ThumbnailUrl = sourceThumbnail,
                        ChannelId = sourceInfo?.ChannelId ?? string.Empty,
                        LastSyncedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                    }).ConfigureAwait(false);
            }
            else
            {
                // Update source metadata on every sync
                await connection.ExecuteAsync(
                    @"UPDATE DMJT_Sources SET Title=@Title, Uploader=@Uploader, ThumbnailUrl=@ThumbnailUrl,
                      ChannelId=@ChannelId, LastSyncedAt=@LastSyncedAt WHERE Id=@Id",
                    new
                    {
                        Id = sourceId,
                        Title = sourceTitle,
                        Uploader = sourceInfo?.Uploader ?? existingSource.Uploader,
                        ThumbnailUrl = !string.IsNullOrEmpty(sourceThumbnail) ? sourceThumbnail : existingSource.ThumbnailUrl,
                        ChannelId = sourceInfo?.ChannelId ?? existingSource.ChannelId,
                        LastSyncedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                    }).ConfigureAwait(false);
            }

            // --- Prepare library folder ---
            var libraryPath = config.LibraryPath;
            if (string.IsNullOrEmpty(libraryPath))
            {
                var appPaths = Plugin.Instance?.ApplicationPaths;
                if (appPaths != null)
                {
                    libraryPath = Path.Combine(appPaths.DataPath, "YouTube");
                }
            }

            if (!string.IsNullOrEmpty(libraryPath) && !Directory.Exists(libraryPath))
            {
                Directory.CreateDirectory(libraryPath);
            }

            var safeSourceTitle = MakeSafeFileName(sourceTitle);
            var sourceDir = string.IsNullOrEmpty(libraryPath) ? null : Path.Combine(libraryPath, safeSourceTitle);

            if (sourceDir != null && !Directory.Exists(sourceDir))
            {
                Directory.CreateDirectory(sourceDir);
            }

            // --- Write tvshow.nfo for the source folder ---
            if (sourceDir != null)
            {
                var tvshowNfoPath = Path.Combine(sourceDir, "tvshow.nfo");
                if (!File.Exists(tvshowNfoPath))
                {
                    var tvshowNfo = $@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<tvshow>
  <title>{Esc(sourceTitle)}</title>
  <plot>{Esc(sourceInfo?.Description ?? string.Empty)}</plot>
  <studio>{Esc(sourceInfo?.Uploader ?? string.Empty)}</studio>
  <thumb aspect=""poster"">{Esc(sourceThumbnail)}</thumb>
  <uniqueid type=""youtube"">{Esc(sourceInfo?.ChannelId ?? sourceInfo?.PlaylistId ?? string.Empty)}</uniqueid>
</tvshow>";
                    await File.WriteAllTextAsync(tvshowNfoPath, tvshowNfo, cancellationToken).ConfigureAwait(false);
                }
            }

            // --- Process individual videos ---
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existingVideo = await connection.QueryFirstOrDefaultAsync<int>(
                    "SELECT 1 FROM DMJT_Videos WHERE Id = @Id", new { Id = item.Id }).ConfigureAwait(false);

                var thumbnailUrl = item.BestThumbnailUrl;
                var uploadDateStr = ParseUploadDate(item.UploadDate);

                if (existingVideo == 0)
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO DMJT_Videos (Id, SourceId, Title, Uploader, Duration, ThumbnailUrl, Description, UploadDate, ViewCount)
                          VALUES (@Id, @SourceId, @Title, @Uploader, @Duration, @ThumbnailUrl, @Description, @UploadDate, @ViewCount)",
                        new
                        {
                            Id = item.Id,
                            SourceId = sourceId,
                            Title = item.Title ?? item.Id,
                            Uploader = item.Uploader ?? sourceInfo?.Uploader ?? string.Empty,
                            Duration = (int)(item.Duration ?? 0),
                            ThumbnailUrl = thumbnailUrl,
                            Description = item.Description ?? string.Empty,
                            UploadDate = uploadDateStr,
                            ViewCount = item.ViewCount ?? 0
                        }).ConfigureAwait(false);
                }

                // --- Generate .strm and .nfo files ---
                if (sourceDir != null)
                {
                    var safeVideoTitle = MakeSafeFileName(item.Title ?? item.Id);
                    var strmPath = Path.Combine(sourceDir, $"{safeVideoTitle} [{item.Id}].strm");
                    var nfoPath = Path.Combine(sourceDir, $"{safeVideoTitle} [{item.Id}].nfo");

                    if (!File.Exists(strmPath))
                    {
                        var serverUrl = config.ServerUrl?.TrimEnd('/') ?? "http://localhost:8096";
                        var streamUrl = $"{serverUrl}/YouTube/Stream?videoId={item.Id}";
                        await File.WriteAllTextAsync(strmPath, streamUrl, cancellationToken).ConfigureAwait(false);
                    }

                    if (!File.Exists(nfoPath))
                    {
                        var runtimeMinutes = (int)((item.Duration ?? 0) / 60);
                        var uploader = item.Uploader ?? sourceInfo?.Uploader ?? string.Empty;

                        var nfoContent = $@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<movie>
  <title>{Esc(item.Title ?? item.Id)}</title>
  <uniqueid type=""youtube"" default=""true"">{item.Id}</uniqueid>
  <studio>{Esc(uploader)}</studio>
  <plot>{Esc(item.Description ?? string.Empty)}</plot>
  <premiered>{Esc(uploadDateStr)}</premiered>
  <aired>{Esc(uploadDateStr)}</aired>
  <runtime>{runtimeMinutes}</runtime>
  <thumb aspect=""poster"">{Esc(thumbnailUrl)}</thumb>
  <thumb aspect=""landscape"">{Esc(thumbnailUrl)}</thumb>
</movie>";
                        await File.WriteAllTextAsync(nfoPath, nfoContent, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            count++;
            progress.Report(100.0 * count / config.SourceUrls.Count);
            _logger.LogInformation("Synced {Count} videos from {Url}", items.Count, url);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(1).Ticks
            }
        };
    }

    private static string MakeSafeFileName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    private static string Esc(string? value)
        => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;

    private static string ParseUploadDate(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length != 8)
        {
            return string.Empty;
        }

        if (DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }
}
