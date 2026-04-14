#pragma warning disable CS1591

namespace Jellyfin.Plugin.ListenBrainz.Resources;

/// <summary>
/// Localized string constants for the ListenBrainz plugin.
/// </summary>
public static class Strings
{
    public const string PluginName = "ListenBrainz";
    public const string Description = "Scrobble your music to ListenBrainz. Send now playing notifications, and love/hate tracks directly from Jellyfin.";
    public const string PluginCategory = "General";
    public const string SettingsTabHeader = "ListenBrainz Settings";
    public const string UserTokenLabel = "ListenBrainz User Token";
    public const string UserTokenHelp = "Enter your ListenBrainz user token. Get one at listenbrainz.org/profile/me/";
    public const string ConnectLabel = "Validate Token";
    public const string ConnectedLabel = "Connected as";
    public const string ScrobblingEnabledLabel = "Enable Scrobbling";
    public const string NowPlayingEnabledLabel = "Enable Now Playing Notifications";
    public const string ScrobblePercentLabel = "Scrobble after";
    public const string MinDurationLabel = "Minimum track duration (seconds)";
    public const string AutoLoveLabel = "Auto-love liked tracks";
    public const string UseAlbumArtistLabel = "Use Album Artist for scrobbling";
    public const string SaveLabel = "Save";
}
