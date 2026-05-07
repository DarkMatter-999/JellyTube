using System;

namespace Jellyfin.Plugin.JellyTube.Data.Models;

/// <summary>
/// Represents a video from a source.
/// </summary>
public class Video
{
    /// <summary>
    /// Gets or sets the video ID (YouTube Video ID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source ID (foreign key to Source).
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the video.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the uploader of the video.
    /// </summary>
    public string Uploader { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration of the video in seconds.
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Gets or sets the thumbnail URL of the video.
    /// </summary>
    public string ThumbnailUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the video.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the upload date of the video.
    /// </summary>
    public DateTime UploadDate { get; set; }
}
