using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jellyfin.Plugin.ListenBrainz.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ListenBrainz.Api;

/// <summary>
/// Client for the ListenBrainz API (https://api.listenbrainz.org/1/).
/// Handles all HTTP communication including scrobbling, now playing,
/// love/hate feedback, and token validation.
///
/// ListenBrainz uses a simpler authentication model than Last.fm:
/// only a user token is required (no API key, secret, or OAuth flow).
/// </summary>
public class ListenBrainzApiClient
{
    private const string BaseUrl = "https://api.listenbrainz.org/1/";

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    private string _userToken;

    /// <summary>
    /// JSON serialization options used for all API requests.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ListenBrainzApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The plugin configuration containing the user token.</param>
    /// <param name="logger">The logger.</param>
    public ListenBrainzApiClient(IHttpClientFactory httpClientFactory, PluginConfiguration config, ILogger logger)
    {
        _httpClient = httpClientFactory.CreateClient("ListenBrainz");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Jellyfin-ListenBrainz/1.0.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _userToken = config.UserToken;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the user token used for authenticated requests.
    /// Updated when the plugin configuration changes.
    /// </summary>
    public string UserToken { get => _userToken; set => _userToken = value; }

    /// <summary>
    /// Validates the current user token against the ListenBrainz API.
    /// </summary>
    /// <returns>
    /// The username if the token is valid, or null if validation failed.
    /// </returns>
    public async Task<string?> ValidateTokenAsync(string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}validate-token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", token);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("ListenBrainz token validation response: {Body}", body);

            if (response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;

                if (root.TryGetProperty("valid", out var validProp) && validProp.GetBoolean())
                {
                    var username = root.GetProperty("user_name").GetString();
                    _logger.LogInformation("ListenBrainz token validated for user {Username}", username);
                    return username;
                }
            }

            _logger.LogWarning("ListenBrainz token validation failed: {StatusCode} - {Body}",
                response.StatusCode, body);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListenBrainz token validation request failed");
            return null;
        }
    }

    /// <summary>
    /// Submits a now-playing notification to ListenBrainz.
    /// The listen_type is "playing" which tells ListenBrainz this is the
    /// currently playing track (not a scrobble).
    /// </summary>
    /// <param name="artist">The track artist name.</param>
    /// <param name="title">The track title.</param>
    /// <param name="album">The album name (optional).</param>
    /// <param name="additionalInfo">Additional metadata as key-value pairs (optional).</param>
    /// <returns>The HTTP response message.</returns>
    public async Task<HttpResponseMessage> UpdateNowPlayingAsync(
        string artist,
        string title,
        string? album = null,
        Dictionary<string, object>? additionalInfo = null)
    {
        if (string.IsNullOrEmpty(_userToken))
        {
            _logger.LogWarning("ListenBrainz: UpdateNowPlaying skipped - no user token");
            return new HttpResponseMessage(System.Net.HttpStatusCode.PreconditionFailed);
        }

        var payload = BuildListenPayload(artist, title, album, additionalInfo);
        var body = BuildSubmitBody("playing_now", payload);

        return await SubmitListensAsync(body);
    }

    /// <summary>
    /// Submits a single scrobble to ListenBrainz.
    /// The listen_type is "single" for individual track submissions.
    /// </summary>
    /// <param name="artist">The track artist name.</param>
    /// <param name="title">The track title.</param>
    /// <param name="album">The album name (optional).</param>
    /// <param name="timestamp">The Unix timestamp when the track started playing.</param>
    /// <param name="additionalInfo">Additional metadata as key-value pairs (optional).</param>
    /// <returns>The HTTP response message.</returns>
    public async Task<HttpResponseMessage> ScrobbleAsync(
        string artist,
        string title,
        string? album = null,
        long timestamp = 0,
        Dictionary<string, object>? additionalInfo = null)
    {
        if (string.IsNullOrEmpty(_userToken))
        {
            _logger.LogWarning("ListenBrainz: Scrobble skipped - no user token");
            return new HttpResponseMessage(System.Net.HttpStatusCode.PreconditionFailed);
        }

        var payload = BuildListenPayload(artist, title, album, additionalInfo);

        // Set the listened_at timestamp for accurate scrobble timing
        if (timestamp > 0)
        {
            payload["listened_at"] = timestamp;
        }
        else
        {
            payload["listened_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        var body = BuildSubmitBody("single", payload);

        return await SubmitListensAsync(body);
    }

    /// <summary>
    /// Submits multiple scrobbles (import mode) to ListenBrainz.
    /// Useful for backfilling or bulk submissions.
    /// </summary>
    /// <param name="listens">The list of listen payloads, each with listened_at.</param>
    /// <returns>The HTTP response message.</returns>
    public async Task<HttpResponseMessage> ImportScrobblesAsync(List<Dictionary<string, object>> listens)
    {
        if (string.IsNullOrEmpty(_userToken) || listens.Count == 0)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.PreconditionFailed);
        }

        var body = new Dictionary<string, object>
        {
            ["listen_type"] = "import",
            ["payload"] = listens
        };

        return await SubmitListensAsync(body);
    }

    /// <summary>
    /// Sends a "love" feedback for a track to ListenBrainz.
    /// Score 1 = love, Score 0 = hate.
    /// </summary>
    /// <param name="artist">The track artist name.</param>
    /// <param name="title">The track title.</param>
    /// <param name="recordingMbid">The MusicBrainz recording ID (optional, preferred).</param>
    public async Task<HttpResponseMessage> LoveTrackAsync(string artist, string title, string? recordingMbid = null)
    {
        return await SendFeedbackAsync(artist, title, recordingMbid, 1);
    }

    /// <summary>
    /// Sends a "hate" feedback for a track to ListenBrainz.
    /// Score 0 = hate.
    /// </summary>
    /// <param name="artist">The track artist name.</param>
    /// <param name="title">The track title.</param>
    /// <param name="recordingMbid">The MusicBrainz recording ID (optional, preferred).</param>
    public async Task<HttpResponseMessage> HateTrackAsync(string artist, string title, string? recordingMbid = null)
    {
        return await SendFeedbackAsync(artist, title, recordingMbid, 0);
    }

    /// <summary>
    /// Removes feedback (love/hate) for a track from ListenBrainz.
    /// </summary>
    /// <param name="artist">The track artist name.</param>
    /// <param name="title">The track title.</param>
    /// <param name="recordingMbid">The MusicBrainz recording ID (optional, preferred).</param>
    public async Task<HttpResponseMessage> RemoveFeedbackAsync(string artist, string title, string? recordingMbid = null)
    {
        return await SendFeedbackAsync(artist, title, recordingMbid, -1);
    }

    /// <summary>
    /// Sends recording feedback (love/hate/remove) to ListenBrainz.
    /// </summary>
    private async Task<HttpResponseMessage> SendFeedbackAsync(
        string artist,
        string title,
        string? recordingMbid,
        int score)
    {
        if (string.IsNullOrEmpty(_userToken))
        {
            _logger.LogWarning("ListenBrainz: Feedback skipped - no user token");
            return new HttpResponseMessage(System.Net.HttpStatusCode.PreconditionFailed);
        }

        var feedback = new Dictionary<string, object>
        {
            ["score"] = score
        };

        if (!string.IsNullOrEmpty(recordingMbid))
        {
            feedback["recording_mbid"] = recordingMbid;
        }
        else
        {
            // Fallback to artist + title when no MBID is available
            feedback["recording_mbid"] = null!;
            feedback["track_metadata"] = new Dictionary<string, object>
            {
                ["artist_name"] = artist,
                ["track_name"] = title
            };
        }

        var json = JsonSerializer.Serialize(feedback, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _logger.LogDebug("ListenBrainz sending feedback: score={Score}, artist={Artist}, title={Title}",
            score, artist, title);

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}feedback/recording-feedback", content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("ListenBrainz feedback failed ({StatusCode}): {Body}",
                    response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("ListenBrainz feedback sent: score={Score}, artist={Artist}, title={Title}",
                    score, artist, title);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListenBrainz feedback request failed");
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Builds a listen payload dictionary with track metadata.
    /// </summary>
    private static Dictionary<string, object> BuildListenPayload(
        string artist,
        string title,
        string? album,
        Dictionary<string, object>? additionalInfo)
    {
        var trackMetadata = new Dictionary<string, object>
        {
            ["artist_name"] = artist,
            ["track_name"] = title
        };

        if (!string.IsNullOrEmpty(album))
        {
            trackMetadata["release_name"] = album;
        }

        // Merge any additional metadata
        if (additionalInfo != null)
        {
            foreach (var kvp in additionalInfo)
            {
                trackMetadata[kvp.Key] = kvp.Value;
            }
        }

        return new Dictionary<string, object>
        {
            ["track_metadata"] = trackMetadata
        };
    }

    /// <summary>
    /// Builds the full submit-listens request body.
    /// </summary>
    private static Dictionary<string, object> BuildSubmitBody(string listenType, Dictionary<string, object> payload)
    {
        return new Dictionary<string, object>
        {
            ["listen_type"] = listenType,
            ["payload"] = new List<Dictionary<string, object>> { payload }
        };
    }

    /// <summary>
    /// Sends a submit-listens request to the ListenBrainz API.
    /// </summary>
    private async Task<HttpResponseMessage> SubmitListensAsync(Dictionary<string, object> body)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}submit-listens")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", _userToken);

        _logger.LogDebug("ListenBrainz submitting listens: {Json}", json);

        try
        {
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("ListenBrainz submit-listens failed ({StatusCode}): {Body}",
                    response.StatusCode, responseBody);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListenBrainz submit-listens request failed");
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }
}
