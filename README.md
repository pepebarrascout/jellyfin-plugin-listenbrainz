# jellyfin-plugin-listenbrainz

A Jellyfin plugin that scrobbles your music to [ListenBrainz](https://listenbrainz.org/).

## Features

- **Scrobbling** — Automatically send scrobbles when tracks reach a configurable threshold
- **Now Playing** — Send now-playing notifications when a track starts
- **Love / Hate** — Send love (score 1) or hate (score 0) feedback to ListenBrainz
- **Auto-Love** — Automatically love tracks that are marked as favorites in Jellyfin
- **Rich Metadata** — Includes MusicBrainz IDs, track numbers, release info, and tags
- **Configuration Panel** — Built-in Jellyfin dashboard page for easy setup

## Requirements

- Jellyfin 10.11.x
- .NET 9.0
- A ListenBrainz account with a [user token](https://listenbrainz.org/profile/)

## Installation

1. Download the latest release `.zip` from the [Releases](https://github.com/paulpbl/jellyfin-plugin-listenbrainz/releases) page
2. Extract the contents to your Jellyfin `plugins/` directory
3. Restart Jellyfin
4. Go to **Dashboard → Plugins → ListenBrainz** and enter your user token

## Building from Source

```bash
git clone https://github.com/paulpbl/jellyfin-plugin-listenbrainz.git
cd jellyfin-plugin-listenbrainz
dotnet restore
dotnet build Jellyfin.Plugin.ListenBrainz/Jellyfin.Plugin.ListenBrainz.csproj -c Release
```

The output DLL will be at:
```
Jellyfin.Plugin.ListenBrainz/bin/Release/net9.0/Jellyfin.Plugin.ListenBrainz.dll
```

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| User Token | — | Your ListenBrainz user token |
| Enable Scrobbling | ✅ | Send scrobbles when tracks reach the threshold |
| Enable Now Playing | ✅ | Send now-playing notifications |
| Scrobble after | 50% | Percentage of track before scrobbling (max 4 min) |
| Min Duration | 30s | Minimum track length to scrobble |
| Auto-love liked tracks | ✅ | Love tracks that are Jellyfin favorites |
| Use Album Artist | ❌ | Scrobble under album artist instead of track artist |

## Architecture

```
Jellyfin Server
  │
  ├─ ISessionManager ──Events──► ListenBrainzScrobbler (IHostedService)
  │   (PlaybackStart)               │
  │   (PlaybackProgress)            ▼
  │   (PlaybackStopped)      ListenBrainzApiClient
  │                               │
  ├─ ListenBrainzPlugin            ▼
  │   (.Configuration)      api.listenbrainz.org
  │
  └─ Dashboard UI ◄── config.html (embedded resource)
```

## API Endpoints Used

| Endpoint | Purpose |
|----------|---------|
| `GET /1/validate-token` | Token validation |
| `POST /1/submit-listens` | Scrobble & now-playing |
| `POST /1/feedback/recording-feedback` | Love / hate feedback |

## License

[MIT](LICENSE)
