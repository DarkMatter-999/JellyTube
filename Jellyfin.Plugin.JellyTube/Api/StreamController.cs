using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Jellyfin.Plugin.JellyTube.YtDlp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTube.Api;

/// <summary>
/// API controller for handling YouTube video stream requests.
/// </summary>
[ApiController]
[Route("YouTube")]
[SuppressMessage("Security", "CA3006:Review code for process command injection vulnerabilities", Justification = "videoId is validated by IsValidYouTubeVideoId regex before use")]
public class StreamController : ControllerBase
{
    private const long BytesPerSecond = 625_000;

    private readonly ILogger<StreamController> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamController"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public StreamController(ILogger<StreamController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a stream for the specified YouTube video.
    /// </summary>
    /// <param name="videoId">The YouTube video ID.</param>
    /// <returns>Redirect or merged stream result.</returns>
    [HttpGet("Stream")]
    [SuppressMessage("Security", "CA3006:Review code for process command injection vulnerabilities", Justification = "videoId is validated by IsValidYouTubeVideoId regex before use")]
    public async Task<ActionResult> GetStream([FromQuery] string videoId)
    {
        if (string.IsNullOrEmpty(videoId))
        {
            return BadRequest("Missing videoId");
        }

        if (!IsValidYouTubeVideoId(videoId))
        {
            return BadRequest("Invalid videoId format");
        }

        var config = Plugin.Instance?.Configuration;
        var db = Plugin.Instance?.DbContext;

        if (config == null || db == null)
        {
            return StatusCode(503, "Plugin not initialized");
        }

        var streamUrls = await GetCachedOrFetchUrlsAsync(videoId, config, db).ConfigureAwait(false);

        if (streamUrls == null || streamUrls.Count == 0)
        {
            return NotFound("Stream URL could not be extracted.");
        }

        if (streamUrls.Count == 1)
        {
            _logger.LogInformation("Redirecting to single muxed stream for {VideoId}", videoId);
            return Redirect(streamUrls[0]);
        }

        _logger.LogInformation("Merging split streams for {VideoId} via ffmpeg (c:copy -> Matroska)", videoId);

        var durationSeconds = await GetVideoDurationAsync(videoId, db).ConfigureAwait(false);
        var rangeHeader = (string?)Request.Headers["Range"];
        var rangeStart = ParseRangeStart(rangeHeader);

        double startTime = 0;
        if (rangeStart.HasValue && durationSeconds > 0)
        {
            startTime = EstimateSeekTime(rangeStart.Value, durationSeconds);
            _logger.LogDebug(
                "Range request: bytes={RangeStart}, duration={Duration}s, seekTime={SeekTime}s",
                rangeStart.Value,
                durationSeconds,
                startTime);
        }

        return PipeMergedStream(streamUrls[0], streamUrls[1], startTime, durationSeconds);
    }

    private async Task<double> GetVideoDurationAsync(string videoId, Data.SqliteDbContext db)
    {
        try
        {
            using var connection = db.GetConnection();
            var duration = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT Duration FROM DMJT_Videos WHERE Id = @Id", new { Id = videoId }).ConfigureAwait(false);
            return duration > 0 ? duration : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get video duration for {VideoId}", videoId);
            return 0;
        }
    }

    private static long? ParseRangeStart(string? rangeHeader)
    {
        if (string.IsNullOrEmpty(rangeHeader))
        {
            return null;
        }

        var match = Regex.Match(rangeHeader, @"^bytes=(\d+)-");
        if (match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var start))
        {
            return start;
        }

        return null;
    }

    private static double EstimateSeekTime(long rangeStart, double durationSeconds)
    {
        if (durationSeconds <= 0 || rangeStart <= 0)
        {
            return 0;
        }

        var totalBytes = (long)(durationSeconds * BytesPerSecond);
        if (totalBytes <= 0)
        {
            return 0;
        }

        var ratio = (double)rangeStart / totalBytes;
        return Math.Max(0, Math.Min(ratio * durationSeconds, durationSeconds - 2));
    }

    private async Task<List<string>?> GetCachedOrFetchUrlsAsync(
        string videoId,
        Configuration.PluginConfiguration config,
        Data.SqliteDbContext db)
    {
        using var connection = db.GetConnection();

        var cached = await connection.QueryFirstOrDefaultAsync<Data.Models.StreamCache>(
            "SELECT * FROM DMJT_StreamCache WHERE VideoId = @VideoId", new { VideoId = videoId }).ConfigureAwait(false);

        if (cached != null && DateTime.TryParse(cached.ExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry) && expiry > DateTime.UtcNow)
        {
            _logger.LogDebug("Using cached stream URLs for {VideoId}", videoId);
            try
            {
                var cachedUrls = JsonSerializer.Deserialize<List<string>>(cached.StreamUrlsJson);
                if (cachedUrls != null && cachedUrls.Count > 0)
                {
                    return cachedUrls;
                }
            }
            catch
            {
            }
        }

        _logger.LogInformation("Fetching stream URLs for {VideoId} via yt-dlp", videoId);
        var wrapper = new YtDlpWrapper(config.YtDlpPath, _logger);

        var formatsToTry = new[]
        {
            config.VideoFormat,
            "bestvideo[height<=1080]+bestaudio/bestvideo+bestaudio/best[height<=1080]/best",
            "best"
        };

        List<string>? urls = null;
        foreach (var fmt in formatsToTry)
        {
            if (string.IsNullOrEmpty(fmt))
            {
                continue;
            }

            _logger.LogDebug("Trying yt-dlp format: {Format}", fmt);
            urls = await wrapper.GetStreamUrlsAsync(videoId, fmt, HttpContext.RequestAborted).ConfigureAwait(false);
            if (urls != null && urls.Count > 0)
            {
                _logger.LogDebug("Got {Count} URL(s) with format: {Format}", urls.Count, fmt);
                break;
            }

            _logger.LogWarning("No URLs returned for format: {Format}", fmt);
        }

        if (urls == null || urls.Count == 0)
        {
            _logger.LogError("Failed to extract any stream URL for {VideoId}", videoId);
            return null;
        }

        var urlsJson = JsonSerializer.Serialize(urls);
        var expiresAt = DateTime.UtcNow.Add(CacheDuration).ToString("o", CultureInfo.InvariantCulture);

        await connection.ExecuteAsync(
            @"INSERT INTO DMJT_StreamCache (VideoId, StreamUrlsJson, ExpiresAt)
              VALUES (@VideoId, @StreamUrlsJson, @ExpiresAt)
              ON CONFLICT(VideoId) DO UPDATE SET StreamUrlsJson=excluded.StreamUrlsJson, ExpiresAt=excluded.ExpiresAt",
            new { VideoId = videoId, StreamUrlsJson = urlsJson, ExpiresAt = expiresAt }).ConfigureAwait(false);

        return urls;
    }

    private FileStreamResult PipeMergedStream(string videoUrl, string audioUrl, double startTimeSeconds = 0, double totalDurationSeconds = 0)
    {
        var ffmpegPath = "/usr/lib/jellyfin-ffmpeg/ffmpeg";
        if (!System.IO.File.Exists(ffmpegPath))
        {
            ffmpegPath = "ffmpeg";
        }

        var args = string.Empty;

        if (startTimeSeconds > 0)
        {
            args += $"-ss {startTimeSeconds} ";
        }

        args += $"-i \"{videoUrl}\" ";

        if (startTimeSeconds > 0)
        {
            args += $"-ss {startTimeSeconds} ";
        }

        args += $"-i \"{audioUrl}\" ";

        // Always tell ffmpeg the expected output duration.
        // This makes the Matroska muxer write the correct Duration in the header,
        // so the player knows the stream length and shows a seek bar.
        // The +2s buffer avoids truncation from timestamp rounding differences.
        if (totalDurationSeconds > 0)
        {
            var remaining = (totalDurationSeconds - startTimeSeconds) + 2;
            args += $"-t {remaining} ";
        }

        args += "-map 0:v:0 -map 1:a:0 "
              + "-c copy "
              + "-fflags +genpts "
              + "-f matroska pipe:1";

        var processStartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = processStartInfo };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogTrace("ffmpeg: {Message}", e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        HttpContext.Response.Headers["Accept-Ranges"] = "bytes";

        if (startTimeSeconds > 0)
        {
            HttpContext.Response.StatusCode = 206;
        }

        HttpContext.Response.RegisterForDispose(process);
        return File(process.StandardOutput.BaseStream, "video/x-matroska");
    }

    /// <summary>
    /// Clears the cached stream URLs for all videos.
    /// </summary>
    /// <returns>OK response on success.</returns>
    [HttpDelete("Cache")]
    public async Task<ActionResult> ClearCache()
    {
        var db = Plugin.Instance?.DbContext;
        if (db == null)
        {
            return StatusCode(503, "Plugin not initialized");
        }

        using var connection = db.GetConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await connection.ExecuteAsync("DELETE FROM DMJT_StreamCache").ConfigureAwait(false);

        _logger.LogInformation("Stream URL cache cleared");
        return Ok("Cache cleared");
    }

    private static bool IsValidYouTubeVideoId(string videoId)
    {
        if (string.IsNullOrEmpty(videoId))
        {
            return false;
        }

        return Regex.IsMatch(videoId, @"^[a-zA-Z0-9_-]{11}$");
    }
}
