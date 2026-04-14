using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.ListenBrainz.Configuration;
using Jellyfin.Plugin.ListenBrainz.Resources;

namespace Jellyfin.Plugin.ListenBrainz;

/// <summary>
/// Main plugin entry point for the ListenBrainz scrobbler.
/// Extends BasePlugin for configuration persistence and implements IHasWebPages
/// to provide the dashboard configuration page.
/// </summary>
public class ListenBrainzPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Gets the singleton instance of the plugin for global access.
    /// Used by the scrobbler service to read configuration.
    /// </summary>
    public static ListenBrainzPlugin? Instance { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ListenBrainzPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer for config persistence.</param>
    /// <param name="logger">The logger instance.</param>
    public ListenBrainzPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<ListenBrainzPlugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        logger.LogInformation("ListenBrainz plugin loaded (v{Version})", Version);
    }

    /// <inheritdoc />
    public override string Name => Strings.PluginName;

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("b8e7f6a5-4d3c-2b1a-0f9e-8d7c6b5a4f3e");

    /// <inheritdoc />
    public override string Description => Strings.Description;

    /// <summary>
    /// Returns the list of web pages provided by this plugin.
    /// The configuration page is served as an embedded HTML resource.
    /// </summary>
    /// <returns>The plugin page info for the configuration page.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}.Configuration.config.html",
                    GetType().Namespace)
            }
        ];
    }
}
