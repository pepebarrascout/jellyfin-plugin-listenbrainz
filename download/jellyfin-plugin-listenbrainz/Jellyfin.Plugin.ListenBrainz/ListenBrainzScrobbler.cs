using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ListenBrainz.Api;
using Jellyfin.Plugin.ListenBrainz.Configuration;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ListenBrainz;

/// <summary>
/// Handles playback event subscription and scrobbling logic for ListenBrainz.
/// Registered as an IHostedService via IPluginServiceRegistrator to ensure
/// automatic instantiation when the Jellyfin server starts.
///
/// Subscribes to ISessionManager events (PlaybackStart, PlaybackStopped, PlaybackProgress)
/// and sends appropriate data to ListenBrainz via the API client.
/// </summary>
public class ListenBrainzScrobbler : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ListenBrainzScrobbler> _logger;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private ListenBrainzApiClient? _apiClient;

    private readonly Dictionary<string, PlaybackTracker> _activeTrackers = new();
    private readonly object _trackerLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ListenBrainzScrobbler"/> class.
    /// </summary>
    /// <param name="sessionManager">The Jellyfin session manager for playback events.</param>
    /// <param name="httpClientFactory">The HTTP client factory for API requests.</param>
    /// <param name="userDataManager">The user data manager for favorite status.</param>
    /// <param name="userManager">The user manager for resolving user by ID.</param>
    /// <param name="logger">The logger.</param>
    public ListenBrainzScrobbler(
        ISessionManager sessionManager,
        IHttpClientFactory httpClientFactory,
        IUserDataManager userDataManager,
        IUserManager userManager,
        ILogger<ListenBrainzScrobbler> logger)
    {
        _sessionManager = sessionManager;
        _httpClientFactory = httpClientFactory;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStarted;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _sessionManager.PlaybackProgress += OnPlaybackProgress;

        _logger.LogInformation("ListenBrainz scrobbler started - listening for playback events");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStarted;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;

        _logger.LogInformation("ListenBrainz scrobbler stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets or creates the API client, always refreshing credentials from current config.
    /// </summary>
    private ListenBrainzApiClient? ApiClient
    {
        get
        {
            var config = ListenBrainzPlugin.Instance?.Configuration;
            if (config == null)
            {
                _logger.LogWarning("ListenBrainz: Cannot access plugin configuration (plugin instance is null)");
                return null;
            }

            if (_apiClient == null)
            {
                _apiClient = new ListenBrainzApiClient(_httpClientFactory, config, _logger);
                _logger.LogInformation("ListenBrainz: API client created with token: {HasToken}",
                    !string.IsNullOrEmpty(config.UserToken) ? "YES" : "NO");
            }
            else
            {
                _apiClient.UserToken = config.UserToken;
            }

            return _apiClient;
        }
    }

    private PluginConfiguration? Config => ListenBrainzPlugin.Instance?.Configuration;

    /// <summary>
    /// Generates a unique tracker key combining device and item identifiers.
    /// </summary>
    private static string GetTrackerKey(string deviceId, Guid itemId)
        => $"{deviceId}:{itemId:N}";

    private async void OnPlaybackStarted(object? sender, PlaybackProgressEventArgs e)
    {
        try
        {
            var config = Config;

            if (e.Item is not Audio audio || config == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(config.UserToken))
            {
                _logger.LogDebug("ListenBrainz: Skipping playback start - no user token configured");
                return;
            }

            // Create a playback tracker for this session
            var tracker = new PlaybackTracker(audio, e.PlaybackPositionTicks ?? 0);
            var key = GetTrackerKey(e.DeviceId, audio.Id);

            lock (_trackerLock)
            {
                _activeTrackers[key] = tracker;
            }

            _logger.LogDebug("ListenBrainz: Playback started - {Artist} - {Title} (key: {Key})",
                audio.Artists?.FirstOrDefault() ?? "Unknown", audio.Name, key);

            // Send now playing notification if enabled
            if (config.NowPlayingEnabled)
            {
                var apiClient = ApiClient;
                if (apiClient != null)
                {
                    var artist = GetArtistName(audio);
                    var album = audio.Album;
                    var title = audio.Name ?? string.Empty;

                    var additionalInfo = BuildAdditionalInfo(audio);

                    var response = await apiClient.UpdateNowPlayingAsync(artist, title, album, additionalInfo);
                    _logger.LogInformation("ListenBrainz now playing sent: {Artist} - {Title} (status: {StatusCode})",
                        artist, title, response.StatusCode);
                }
                else
                {
                    _logger.LogWarning("ListenBrainz: Cannot send now playing - API client is null");
                }
            }

            // Auto-love if track is favorited and auto-love is enabled
            if (config.AutoLoveLikedTracks)
            {
                await TrySendLoveFeedbackAsync(e, audio, config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle playback start for ListenBrainz");
        }
    }

    private async void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
    {
        try
        {
            var config = Config;

            if (e.Item is not Audio audio || config == null)
            {
                return;
            }

            if (!config.ScrobblingEnabled || string.IsNullOrEmpty(config.UserToken))
            {
                return;
            }

            var key = GetTrackerKey(e.DeviceId, audio.Id);
            PlaybackTracker? tracker;

            lock (_trackerLock)
            {
                if (!_activeTrackers.TryGetValue(key, out tracker))
                {
                    return;
                }
            }

            // Skip if already scrobbled during a previous progress event
            if (tracker.Scrobbled)
            {
                return;
            }

            var positionTicks = e.PlaybackPositionTicks ?? 0;
            var durationTicks = audio.RunTimeTicks ?? 0;

            if (durationTicks == 0)
            {
                return;
            }

            var durationSeconds = durationTicks / 10_000_000;
            if (durationSeconds < config.MinDurationSeconds)
            {
                return;
            }

            var positionSeconds = positionTicks / 10_000_000;

            // ListenBrainz scrobble rules: min(duration * percent / 100, 240 seconds)
            // The ListenBrainz server also enforces a minimum of 4 minutes (240s)
            var minScrobbleSeconds = Math.Min((int)(durationSeconds * config.ScrobblePercent / 100.0), 240);

            if (positionSeconds >= minScrobbleSeconds)
            {
                tracker.Scrobbled = true;

                var apiClient = ApiClient;
                if (apiClient != null)
                {
                    var artist = GetArtistName(audio);
                    var album = audio.Album;
                    var title = audio.Name ?? string.Empty;
                    var timestamp = tracker.StartTimeUnix;

                    var additionalInfo = BuildAdditionalInfo(audio);

                    var response = await apiClient.ScrobbleAsync(artist, title, album, timestamp, additionalInfo);
                    _logger.LogInformation("ListenBrainz scrobble sent: {Artist} - {Title} at {Timestamp} (status: {StatusCode})",
                        artist, title, timestamp, response.StatusCode);
                }
                else
                {
                    _logger.LogWarning("ListenBrainz: Cannot scrobble - API client is null");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle playback progress for ListenBrainz");
        }
    }

    private async void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        try
        {
            var config = Config;

            if (e.Item is not Audio audio || config == null)
            {
                return;
            }

            var key = GetTrackerKey(e.DeviceId, audio.Id);

            // Clean up tracker even if scrobbling is disabled
            if (!config.ScrobblingEnabled || string.IsNullOrEmpty(config.UserToken))
            {
                lock (_trackerLock)
                {
                    _activeTrackers.Remove(key);
                }

                return;
            }

            bool wasScrobbled;
            long trackStartTimeUnix = 0;

            lock (_trackerLock)
            {
                if (!_activeTrackers.TryGetValue(key, out var tracker))
                {
                    _logger.LogDebug("ListenBrainz: Playback stopped for {Title} but no tracker found - skipping",
                        audio.Name);
                    return;
                }

                wasScrobbled = tracker.Scrobbled;
                trackStartTimeUnix = tracker.StartTimeUnix;
                _activeTrackers.Remove(key);
            }

            // Skip if already scrobbled during a playback progress event
            if (wasScrobbled)
            {
                return;
            }

            // Fallback: scrobble at stop if the progress event didn't trigger it
            var positionTicks = e.PlaybackPositionTicks ?? 0;
            var durationTicks = audio.RunTimeTicks ?? 0;

            if (durationTicks == 0)
            {
                return;
            }

            var durationSeconds = durationTicks / 10_000_000;
            if (durationSeconds < config.MinDurationSeconds)
            {
                return;
            }

            var positionSeconds = positionTicks / 10_000_000;
            var minScrobbleSeconds = Math.Min((int)(durationSeconds * config.ScrobblePercent / 100.0), 240);

            if (positionSeconds >= minScrobbleSeconds)
            {
                var apiClient = ApiClient;
                if (apiClient != null)
                {
                    var artist = GetArtistName(audio);
                    var album = audio.Album;
                    var title = audio.Name ?? string.Empty;
                    var timestamp = trackStartTimeUnix;

                    var additionalInfo = BuildAdditionalInfo(audio);

                    var response = await apiClient.ScrobbleAsync(artist, title, album, timestamp, additionalInfo);
                    _logger.LogInformation("ListenBrainz scrobble sent on stop: {Artist} - {Title} (status: {StatusCode})",
                        artist, title, response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle playback stop for ListenBrainz");
        }
    }

    /// <summary>
    /// Attempts to send a "love" feedback for favorited tracks.
    /// </summary>
    private async Task TrySendLoveFeedbackAsync(PlaybackProgressEventArgs e, Audio audio, PluginConfiguration config)
    {
        try
        {
            var userId = e.Session?.UserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return;
            }

            var user = _userManager.GetUserById(userId);
            if (user == null) return;
            var userData = _userDataManager.GetUserData(user, audio);
            if (userData == null || !userData.IsFavorite)
            {
                return;
            }

            var apiClient = ApiClient;
            if (apiClient == null)
            {
                return;
            }

            var artist = GetArtistName(audio);
            var title = audio.Name ?? string.Empty;

            var response = await apiClient.LoveTrackAsync(artist, title);
            _logger.LogInformation("ListenBrainz auto-love sent: {Artist} - {Title} (status: {StatusCode})",
                artist, title, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListenBrainz: Failed to send auto-love feedback");
        }
    }

    /// <summary>
    /// Builds additional info metadata from the audio item.
    /// Includes MusicBrainz IDs and other useful metadata.
    /// </summary>
    private static Dictionary<string, object>? BuildAdditionalInfo(Audio audio)
    {
        var info = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(audio.ProviderIds?.GetValueOrDefault("MusicBrainzTrack")))
        {
            info["track_mbid"] = audio.ProviderIds["MusicBrainzTrack"];
        }

        if (!string.IsNullOrEmpty(audio.ProviderIds?.GetValueOrDefault("MusicBrainzAlbum")))
        {
            info["release_mbid"] = audio.ProviderIds["MusicBrainzAlbum"];
        }

        if (!string.IsNullOrEmpty(audio.ProviderIds?.GetValueOrDefault("MusicBrainzArtist")))
        {
            info["artist_mbids"] = new[] { audio.ProviderIds["MusicBrainzArtist"] };
        }

        if (audio.IndexNumber.HasValue)
        {
            info["tracknumber"] = audio.IndexNumber.Value;
        }

        if (!string.IsNullOrEmpty(audio.Genres?.FirstOrDefault()))
        {
            info["tags"] = audio.Genres.Where(g => !string.IsNullOrEmpty(g)).ToArray();
        }

        return info.Count > 0 ? info : null;
    }

    /// <summary>
    /// Gets the artist name for scrobbling, respecting configuration.
    /// </summary>
    /// <param name="audio">The audio item.</param>
    /// <returns>The artist name.</returns>
    private string GetArtistName(Audio audio)
    {
        var config = Config;
        if (config != null && config.UseAlbumArtist)
        {
            var albumArtist = audio.AlbumArtists?.FirstOrDefault();
            if (!string.IsNullOrEmpty(albumArtist))
            {
                return albumArtist;
            }
        }

        return audio.Artists?.FirstOrDefault() ?? audio.GetTopParent()?.Name ?? "Unknown Artist";
    }

    /// <summary>
    /// Invalidates the API client so it will be recreated with fresh config on next use.
    /// </summary>
    public void InvalidateApiClient()
    {
        _apiClient = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sessionManager.PlaybackStart -= OnPlaybackStarted;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
    }
}

/// <summary>
/// Tracks playback state for a single track to detect scrobble eligibility.
/// Prevents duplicate scrobble submissions across progress events and stop events.
/// </summary>
internal class PlaybackTracker
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackTracker"/> class.
    /// </summary>
    /// <param name="audio">The audio item being played.</param>
    /// <param name="positionTicks">The starting position in ticks.</param>
    public PlaybackTracker(Audio audio, long positionTicks)
    {
        TrackId = audio.Id.ToString("N", CultureInfo.InvariantCulture);
        DurationTicks = audio.RunTimeTicks ?? 0;
        StartPositionTicks = positionTicks;
        // Calculate the approximate Unix timestamp when playback started.
        // This accounts for the user potentially seeking into the track.
        StartTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (positionTicks / 10_000_000);
        Scrobbled = false;
    }

    /// <summary>
    /// Gets the unique track identifier (Jellyfin item ID without dashes).
    /// </summary>
    public string TrackId { get; }

    /// <summary>
    /// Gets the total track duration in ticks.
    /// </summary>
    public long DurationTicks { get; }

    /// <summary>
    /// Gets or sets the starting position in ticks.
    /// </summary>
    public long StartPositionTicks { get; set; }

    /// <summary>
    /// Gets the approximate Unix timestamp when playback started.
    /// Used to calculate accurate scrobble timestamps for ListenBrainz.
    /// </summary>
    public long StartTimeUnix { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this track has already been scrobbled.
    /// Prevents duplicate submissions from multiple progress events.
    /// </summary>
    public bool Scrobbled { get; set; }
}
