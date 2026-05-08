using System;

namespace Jellyfin.Plugin.JellyTube.Data.Models;

/// <summary>
/// Represents a YouTube media source.
/// </summary>
public class Source
{
    /// <summary>
    /// Gets or sets the unique identifier for the source.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the URL of the source.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the source.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the source.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the uploader name for the source.
    /// </summary>
    public string Uploader { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the source.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the thumbnail URL for the source.
    /// </summary>
    public string ThumbnailUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel ID associated with the source.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin library ID associated with this source.
    /// </summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the source was last synced.
    /// </summary>
    public DateTime LastSyncedAt { get; set; } = DateTime.MinValue;
}
