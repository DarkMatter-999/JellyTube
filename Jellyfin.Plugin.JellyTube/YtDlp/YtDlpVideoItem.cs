using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Jellyfin.Data.Entities.Libraries;

namespace Jellyfin.Plugin.JellyTube.YtDlp;

/// <summary>
/// Represents a video item returned by yt-dlp, containing metadata for a single video.
/// </summary>
/// <remarks>
/// This class is used to deserialize video information from yt-dlp JSON output.
/// It includes both individual video fields and playlist-level fields that are present
/// when using the --flat-playlist option. The class provides a convenient property
/// to retrieve the best available thumbnail.
/// </remarks>
public class YtDlpVideoItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the video.
    /// </summary>
    /// <remarks>
    /// This is typically the video ID on the platform (e.g., YouTube video ID).
    /// </remarks>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the video.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the uploader name of the video.
    /// </summary>
    [JsonPropertyName("uploader")]
    public string? Uploader { get; set; }

    /// <summary>
    /// Gets or sets the duration of the video in seconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>
    /// Gets or sets the URL of the thumbnail image.
    /// </summary>
    /// <remarks>
    /// This is typically a single thumbnail URL. For better quality, use the
    /// <see cref="Thumbnails"/> collection and <see cref="BestThumbnailUrl"/> property.
    /// </remarks>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>
    /// Gets or sets the collection of available thumbnails with different resolutions.
    /// </summary>
    /// <remarks>
    /// Multiple thumbnail sizes are typically available. The <see cref="BestThumbnailUrl"/>
    /// property automatically selects the largest available thumbnail.
    /// </remarks>
    [JsonPropertyName("thumbnails")]
    [SuppressMessage("Design", "CA2227:Collection properties should be read only", Justification = "Jellyfin XML serialization requires a setter")]
    public Collection<YtDlpThumbnail>? Thumbnails { get; set; }

    /// <summary>
    /// Gets or sets the description of the video.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the upload date of the video in YYYYMMDD format.
    /// </summary>
    /// <remarks>
    /// The date is typically in the format YYYYMMDD (e.g., "20230615" for June 15, 2023).
    /// </remarks>
    [JsonPropertyName("upload_date")]
    public string? UploadDate { get; set; }

    /// <summary>
    /// Gets or sets the view count of the video.
    /// </summary>
    [JsonPropertyName("view_count")]
    public long? ViewCount { get; set; }

    /// <summary>
    /// Gets or sets the type of the entry (e.g., "url" for a video in a flat playlist).
    /// </summary>
    /// <remarks>
    /// When using --flat-playlist, this field indicates the type of entry.
    /// Only entries with type "url" represent actual videos to be downloaded.
    /// </remarks>
    [JsonPropertyName("_type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the parent playlist (when using --flat-playlist).
    /// </summary>
    /// <remarks>
    /// This field is present on each flat-playlist entry and contains the playlist title.
    /// It is null for non-playlist sources (e.g., individual videos or channels).
    /// </remarks>
    [JsonPropertyName("playlist_title")]
    public string? PlaylistTitle { get; set; }

    /// <summary>
    /// Gets or sets the uploader of the parent playlist (when using --flat-playlist).
    /// </summary>
    /// <remarks>
    /// This field is present on each flat-playlist entry and contains the playlist uploader name.
    /// </remarks>
    [JsonPropertyName("playlist_uploader")]
    public string? PlaylistUploader { get; set; }

    /// <summary>
    /// Gets or sets the channel ID of the parent playlist (when using --flat-playlist).
    /// </summary>
    /// <remarks>
    /// This field is present on each flat-playlist entry for YouTube playlists.
    /// </remarks>
    [JsonPropertyName("playlist_channel_id")]
    public string? PlaylistChannelId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the parent playlist (when using --flat-playlist).
    /// </summary>
    /// <remarks>
    /// This field is present on each flat-playlist entry and contains the playlist ID.
    /// </remarks>
    [JsonPropertyName("playlist_id")]
    public string? PlaylistId { get; set; }

    /// <summary>
    /// Gets or sets the channel ID of the video uploader.
    /// </summary>
    /// <remarks>
    /// This is typically the YouTube channel ID of the uploader.
    /// </remarks>
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    /// <summary>
    /// Gets the best available thumbnail URL.
    /// </summary>
    /// <remarks>
    /// This property automatically selects the largest thumbnail from the available thumbnails array,
    /// falling back to the single thumbnail URL if the array is not available, and finally
    /// returning an empty string if no thumbnail is available.
    /// </remarks>
    /// <returns>
    /// The URL of the best (largest) available thumbnail, or an empty string if none are available.
    /// </returns>
    public string BestThumbnailUrl =>
        Thumbnails?.OrderByDescending(t => t.Width * t.Height).FirstOrDefault()?.Url
        ?? Thumbnail
        ?? string.Empty;
}
