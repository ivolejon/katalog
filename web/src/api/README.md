# src/api - Katalog API client

## Status: handwritten, contract available

The backend generates and commits `contracts/katalog-api/openapi.json`. Until
the generated client is adopted, `src/api/` ships a **handwritten typed client**
matching the contract's endpoint surface (arch report 3.6):

| Endpoint | Client function |
|---|---|
| `GET /api/labels` | `api.listLabels()` |
| `POST /api/labels` | `api.createLabel(input)` |
| `DELETE /api/labels/{id}` | `api.deleteLabel(id)` |
| `GET /api/labels/{id}` | `api.getLabel(id, includeReleases?)` |
| `GET /api/labels/{id}/releases` | `api.getLabelReleases(id, page, pageSize)` |
| `GET /api/labels/search` | `api.searchLabels({ q })` |
| `GET /api/labels/{id}/releases` | (label detail already embeds releases) |

## Spotify Connect (`src/api/spotify.ts`, exported as `spotifyApi`)

Sign-in with the user's own Spotify account, then play a release on one of *their*
devices. These calls use the signed-in user's Spotify token, which the backend
keeps server-side - the browser never receives a token, and the Spotify app's
client id/secret never reach this app.

| Endpoint | Client function |
|---|---|
| `GET /api/spotify/me` | `spotifyApi.session()` |
| `GET /api/spotify/devices` | `spotifyApi.devices()` |
| `GET /api/spotify/playback` | `spotifyApi.playbackState()` |
| `PUT /api/spotify/playback/play` | `spotifyApi.play(spotifyAlbumId, deviceId)` |
| `PUT /api/spotify/playback/pause` | `spotifyApi.pause(deviceId)` |
| `POST /api/spotify/auth/logout` | `spotifyApi.logout()` |

`GET /api/spotify/auth/login` is a redirect the *browser* follows (a plain
`window.location.assign('/api/spotify/auth/login')`), not a fetch: the backend
redirects to Spotify's consent screen and calls back to
`/api/spotify/auth/callback`, which sends the browser back to the app with
`?spotify=connected` or `?spotify=failed&reason=...`. `src/stores/spotify.ts`
reads and cleans that query parameter on start.

## Switching to the generated client

To adopt the committed contract as a generated client, run:

```sh
npm run generate:client
```

That runs `@hey-api/openapi-ts` and writes types + client functions to
`src/api/generated/`. The generated `sdk.gen.ts` / `types.gen.ts` then replace
`http.ts` + `types.ts`; swap `api/labels.ts` to call the generated functions and
delete this file's handwritten-client note. Keep the `/api` base path and error
handling consistent so views and stores do not change.

`API_CLIENT_ORIGIN` in `index.ts` tracks which client is active for the PR
review and for the no-mistakes gate.
