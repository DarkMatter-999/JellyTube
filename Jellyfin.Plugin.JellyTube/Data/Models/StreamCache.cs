namespace Jellyfin.Plugin.JellyTube.Data.Models;

/// <summary>
/// Represents a cached stream containing video URLs and expiration information.
/// </summary>
public class StreamCache
{
    /// <summary>
    /// Gets or sets the video identifier.
    /// </summary>
    public string VideoId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON array of stream URLs (1 = muxed, 2 = video+audio separate).
    /// </summary>
    public string StreamUrlsJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ISO 8601 UTC expiry timestamp string.
    /// </summary>
    public string ExpiresAt { get; set; } = string.Empty;
}
