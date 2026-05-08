using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.JellyTube.Configuration;
using Jellyfin.Plugin.JellyTube.Data;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.JellyTube;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        ApplicationPaths = applicationPaths;
        DbContext = new SqliteDbContext(applicationPaths);
    }

    /// <inheritdoc />
    public override string Name => "JellyTube";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("1221bde2-5440-4325-846f-b1908f65ba52");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the application paths.
    /// </summary>
    public new IApplicationPaths ApplicationPaths { get; private set; }

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public SqliteDbContext DbContext { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
