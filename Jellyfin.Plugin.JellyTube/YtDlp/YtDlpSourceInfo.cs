using System;

namespace Jellyfin.Plugin.JellyTube.YtDlp;

/// <summary>
/// Represents source-level metadata extracted from yt-dlp output.
/// </summary>
/// <remarks>
/// This class aggregates information about a video source (channel, playlist, or individual video)
/// that is extracted from yt-dlp responses. It is typically populated from flat-playlist entries
/// or from dedicated source metadata queries. The data in this class represents the "container"
/// of videos rather than individual video details.
/// </remarks>
public class YtDlpSourceInfo
{
    /// <summary>
    /// Gets or sets the title of the source (channel name, playlist name, etc.).
    /// </summary>
    /// <remarks>
    /// This is the primary display name for the source and should be used for UI presentation.
    /// </remarks>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the uploader or owner name of the source.
    /// </summary>
    /// <remarks>
    /// For a playlist, this is the account that created the playlist.
    /// For a channel, this is the channel owner's display name.
    /// </remarks>
    public string Uploader { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the source.
    /// </summary>
    /// <remarks>
    /// This may contain HTML or markdown formatting depending on the source.
    /// Note: When using yt-dlp's flat-playlist mode, description may not be available.
    /// </remarks>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the source's thumbnail or banner image.
    /// </summary>
    /// <remarks>
    /// This typically represents the best quality thumbnail available for the source.
    /// </remarks>
    public string ThumbnailUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique channel identifier for the source.
    /// </summary>
    /// <remarks>
    /// For YouTube sources, this is the channel ID. For other platforms, it may represent
    /// a different form of unique identifier for the content creator.
    /// </remarks>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique playlist identifier for the source.
    /// </summary>
    /// <remarks>
    /// For playlist sources, this contains the playlist ID. For channel or individual video sources,
    /// this may be empty or contain the source's primary identifier.
    /// </remarks>
    public string PlaylistId { get; set; } = string.Empty;
}
