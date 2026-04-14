using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ListenBrainz;

/// <summary>
/// Registers the ListenBrainz scrobbler as a hosted service in Jellyfin's DI container.
/// This class must be separate from the plugin class for Jellyfin's plugin discovery.
/// </summary>
public class ListenBrainzPluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers the ListenBrainz scrobbler as a hosted service.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register with.</param>
    /// <param name="applicationHost">The application host.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHostedService<ListenBrainzScrobbler>();
    }
}
