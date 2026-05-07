using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTube.YtDlp;

/// <summary>
/// Wrapper class for executing yt-dlp commands and parsing their output.
/// </summary>
/// <remarks>
/// This class provides a high-level interface to interact with the yt-dlp executable,
/// handling process execution, JSON deserialization, and error logging. It abstracts away
/// the details of spawning processes and parsing yt-dlp's JSON output format.
/// The wrapper supports various operations including:
/// - Fetching source-level metadata (channels, playlists)
/// - Retrieving playlist items
/// - Extracting stream URLs for video playback
/// All operations are async and support cancellation via CancellationToken.
/// </remarks>
public class YtDlpWrapper
{
    private readonly string _executablePath;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="YtDlpWrapper"/> class.
    /// </summary>
    /// <param name="executablePath">The full path to the yt-dlp executable.</param>
    /// <param name="logger">The logger instance for recording warnings and errors.</param>
    /// <remarks>
    /// The executable path should point to the yt-dlp binary on the system.
    /// The logger is used to record all warnings, errors, and debug information
    /// during yt-dlp command execution.
    /// </remarks>
    public YtDlpWrapper(string executablePath, ILogger logger)
    {
        _executablePath = executablePath;
        _logger = logger;
    }

    /// <summary>
    /// Fetches source-level metadata for a URL using a fast --playlist-items 0 call.
    /// </summary>
    /// <param name="url">The URL of the video, channel, or playlist to fetch metadata for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="YtDlpSourceInfo"/> object containing the source metadata, or null if the operation fails.
    /// </returns>
    /// <remarks>
    /// This method uses the --playlist-items 0 flag to fetch only the metadata without downloading
    /// any video data, making it very fast. The --flat-playlist flag is used to extract playlist
    /// information from a single item's metadata.
    /// </remarks>
    public async Task<YtDlpSourceInfo?> GetSourceMetadataAsync(string url, CancellationToken cancellationToken)
    {
        var args = $"--dump-single-json --flat-playlist --playlist-items 0 --js-runtimes node \"{url}\"";
        var lines = await ExecuteCommandAsync(args, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            return null;
        }

        try
        {
            var json = string.Join("\n", lines);
            var item = JsonSerializer.Deserialize<YtDlpVideoItem>(json);
            if (item == null)
            {
                return null;
            }

            return new YtDlpSourceInfo
            {
                Title = item.Title ?? item.PlaylistTitle ?? string.Empty,
                Uploader = item.Uploader ?? item.PlaylistUploader ?? string.Empty,
                Description = item.Description ?? string.Empty,
                ThumbnailUrl = item.BestThumbnailUrl,
                ChannelId = item.ChannelId ?? item.PlaylistChannelId ?? string.Empty,
                PlaylistId = item.Id ?? item.PlaylistId ?? string.Empty
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse source metadata JSON");
            return null;
        }
    }

    /// <summary>
    /// Executes a yt-dlp command and returns the output lines.
    /// </summary>
    /// <param name="arguments">The command-line arguments to pass to yt-dlp.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A list of output lines from yt-dlp's standard output.
    /// </returns>
    /// <remarks>
    /// This method spawns a new process running the yt-dlp executable with the specified arguments.
    /// It captures both standard output and standard error, logging any error messages or warnings.
    ///
    /// If the process exits with a non-zero exit code, an error is logged but the method still returns
    /// whatever output was captured. This allows the caller to decide how to handle partial or failed results.
    ///
    /// The method is async and respects the provided cancellation token.
    /// </remarks>
    private async Task<List<string>> ExecuteCommandAsync(string arguments, CancellationToken cancellationToken)
    {
        var outputLines = new List<string>();

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = processStartInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    outputLines.Add(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogWarning("yt-dlp stderr: {Message}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogError("yt-dlp exited with code {Code}", process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute yt-dlp");
        }

        return outputLines;
    }

    /// <summary>
    /// Fetches all flat-playlist entries from a URL.
    /// </summary>
    /// <param name="url">The URL of the playlist or channel to fetch items from.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A tuple containing:
    /// - A list of <see cref="YtDlpVideoItem"/> objects representing each video in the playlist
    /// - A <see cref="YtDlpSourceInfo"/> object containing source metadata (if available), or null.
    /// </returns>
    /// <remarks>
    /// This method uses yt-dlp's --flat-playlist and --dump-json options to retrieve all items
    /// in a playlist without downloading the actual videos. The method returns one line of JSON
    /// per video, allowing for streaming processing of large playlists.
    ///
    /// The source metadata is extracted from the first valid entry's playlist_* fields, avoiding
    /// the need for a separate HTTP request. If the source is not a playlist, the source info
    /// will be null.
    ///
    /// Items with type != "url" or missing IDs are filtered out and not included in the results.
    /// </remarks>
    public async Task<(List<YtDlpVideoItem> Items, YtDlpSourceInfo? SourceInfo)> GetPlaylistItemsAsync(
        string url, CancellationToken cancellationToken)
    {
        var args = $"--dump-json --flat-playlist --js-runtimes node \"{url}\"";
        var lines = await ExecuteCommandAsync(args, cancellationToken).ConfigureAwait(false);

        var items = new List<YtDlpVideoItem>();
        YtDlpSourceInfo? sourceInfo = null;

        foreach (var line in lines)
        {
            try
            {
                var item = JsonSerializer.Deserialize<YtDlpVideoItem>(line);
                if (item == null || string.IsNullOrEmpty(item.Id) || item.Type != "url")
                {
                    continue;
                }

                items.Add(item);

                // Extract source info once from the first valid entry's playlist_* fields
                if (sourceInfo == null && item.PlaylistTitle != null)
                {
                    sourceInfo = new YtDlpSourceInfo
                    {
                        Title = item.PlaylistTitle,
                        Uploader = item.PlaylistUploader ?? string.Empty,
                        Description = string.Empty, // not available in flat mode
                        ThumbnailUrl = item.BestThumbnailUrl,
                        ChannelId = item.PlaylistChannelId ?? string.Empty,
                        PlaylistId = item.PlaylistId ?? string.Empty
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Failed to parse JSON line from yt-dlp: {Line}", line);
            }
        }

        return (items, sourceInfo);
    }

    /// <summary>
    /// Fetches the stream URLs for a specific video that can be used for playback.
    /// </summary>
    /// <param name="videoId">The unique identifier of the video (typically a YouTube video ID).</param>
    /// <param name="format">The yt-dlp format string to request (e.g., "best"). Defaults to "best" if empty.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A list of stream URLs suitable for playback. Typically contains one or more URLs depending on the format requested.
    /// </returns>
    /// <remarks>
    /// This method uses several optimizations to speed up execution:
    /// - --no-check-certificates: Skips SSL verification (safe for CDN URLs from yt-dlp)
    /// - --extractor-args skip=hls,dash: Avoids enumerating HLS/DASH manifests (~50% faster)
    /// - --no-playlist: Ensures only the single video is targeted, not any associated playlist
    /// - --js-runtimes node: Uses Node.js for JavaScript evaluation if needed
    ///
    /// The returned URLs can be directly used for video playback. Multiple URLs may be returned
    /// if the format specifies alternatives or if HLS/DASH manifests are included.
    /// </remarks>
    public async Task<List<string>> GetStreamUrlsAsync(string videoId, string format, CancellationToken cancellationToken)
    {
        var url = $"https://www.youtube.com/watch?v={videoId}";
        var formatArg = string.IsNullOrEmpty(format) ? "best" : format;

        // Speed optimizations:
        // --no-check-certificates  : skips SSL verification (safe for CDN URLs)
        // --extractor-args skip=hls,dash : avoids enumerating HLS/DASH manifests, ~50% faster
        // --no-playlist            : ensures we only target the single video
        var args = $"-f \"{formatArg}\" -g "
                 + "--no-check-certificates "
                 + "--extractor-args \"youtube:skip=hls,dash\" "
                 + "--js-runtimes node "
                 + "--no-playlist "
                 + $"\"{url}\"";

        return await ExecuteCommandAsync(args, cancellationToken).ConfigureAwait(false);
    }
}
