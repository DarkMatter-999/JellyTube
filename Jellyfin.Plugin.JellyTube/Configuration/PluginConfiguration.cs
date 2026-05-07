using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyTube.Configuration;

/// <summary>
/// The configuration options.
/// </summary>
public enum SomeOptions
{
    /// <summary>
    /// Option one.
    /// </summary>
    OneOption,

    /// <summary>
    /// Second option.
    /// </summary>
    AnotherOption
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        YtDlpPath = "yt-dlp";
        VideoFormat = "res:1080,ext:mp4:m4a";
        SourceUrls = new Collection<string>();
    }

    /// <summary>
    /// Gets or sets the path to the yt-dlp executable.
    /// </summary>
    public string YtDlpPath { get; set; }

    /// <summary>
    /// Gets or sets the collection of YouTube source URLs (channels, playlists, videos).
    /// </summary>
    [SuppressMessage("Design", "CA2227:Collection properties should be read only", Justification = "Jellyfin XML serialization requires a setter")]
    public Collection<string> SourceUrls { get; set; }

    /// <summary>
    /// Gets or sets the preferred video format for yt-dlp.
    /// </summary>
    public string VideoFormat { get; set; }
}
