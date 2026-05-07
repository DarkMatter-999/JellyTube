using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyTube.YtDlp;

/// <summary>
/// Represents thumbnail metadata from yt-dlp output.
/// </summary>
/// <remarks>
/// This class is used to deserialize thumbnail information from yt-dlp JSON responses.
/// Thumbnails are typically provided in multiple resolutions, allowing selection of the best quality.
/// </remarks>
public class YtDlpThumbnail
{
    /// <summary>
    /// Gets or sets the URL of the thumbnail image.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the width of the thumbnail image in pixels.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the thumbnail image in pixels.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }
}
