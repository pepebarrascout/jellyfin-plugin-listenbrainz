using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ListenBrainz.Configuration;

/// <summary>
/// Plugin configuration for ListenBrainz scrobbling.
/// All properties are automatically persisted by Jellyfin as XML.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the ListenBrainz user token.
    /// This is the only credential needed — no API key or secret required.
    /// </summary>
    public string UserToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ListenBrainz username (populated after token validation).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether scrobbling is enabled.
    /// </summary>
    public bool ScrobblingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether now playing notifications are enabled.
    /// </summary>
    public bool NowPlayingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the percentage of track playback required before scrobbling.
    /// ListenBrainz servers enforce a minimum of 4 minutes of playback,
    /// so the effective threshold is min(duration * percent / 100, 240 seconds).
    /// </summary>
    public int ScrobblePercent { get; set; } = 50;

    /// <summary>
    /// Gets or sets a value indicating whether liked (favorite) tracks should
    /// be automatically sent as "love" feedback to ListenBrainz.
    /// </summary>
    public bool AutoLoveLikedTracks { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum track duration in seconds for a track to be scrobbled.
    /// Tracks shorter than this will be ignored.
    /// </summary>
    public int MinDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether to use the album artist
    /// instead of the track artist for scrobbling submissions.
    /// </summary>
    public bool UseAlbumArtist { get; set; } = false;
}
