# Katalog - Domain

## The product

Follow record labels ("labels") via Spotify and see their releases. The user
adds a label (name + linked artists), and the app tracks new albums/singles
from those artists.

Decisions recorded with the captain:

- **Single user** - no multi-tenant design, no user accounts.
- **User adds labels manually** - via Spotify artist search in the add-label
  dialog; the label entity is app-owned.
- **No tracks stored** - albums/artists/labels only.
- **No Spotify user OAuth in the MVP** - all Spotify catalog data is fetched
  with a client-credentials app token. Consequences: the user's personal
  Spotify follows/playlists are not part of Katalog.
- **MusicBrainz deferred** - not integrated yet; reserved as enrichment
  fallback.
- **Frontend theme** - shadcn-vue preset `a1AhVxI` (reka-maia).

## Why labels are an app-owned model

Spotify's Web API has **no label entity, no label endpoint, and no way to
follow a label**. The only label metadata is the `label` free-text field on the
full album object, which the current OpenAPI spec marks `deprecated`.
Artist-album lists and search results return simplified album objects without
it, and the batch albums endpoint is also deprecated.

Therefore "following a label" is implemented as:

1. A label row in the app database (created by the user).
2. Artists linked to that label (manual selection via artist search today;
   album-metadata derivation later - see planned backend).
3. A release poller per linked artist when the backend lands
   (`GET /artists/{id}/albums?include_groups=album,single`, idempotent upsert
   on Spotify album id).

Spotify policy constraints that shape the domain: no indefinite storage of
Spotify content (metadata retention/cleanup required), display must link back
to Spotify with attribution, and dev-mode quota (~5 authenticated users / rate
limits with `Retry-After`) bounds polling frequency.

## Current frontend behavior

- `LabelsView`: list of followed labels; add-label dialog with debounced
  Spotify artist search (`useArtistSearch`, 350 ms, empties reset without an
  API call); follow/unfollow toggles.
- `LabelDetailView`: tabs for **Artists** and **Releases**; album cards link
  to Spotify.
- Empty/error/skeleton states everywhere; stale-response guards in the Pinia
  store so an in-flight refresh cannot overwrite newer local state.
- No login: the app opens straight to the landing page and `/labels`.

## API surface (agreed, handwritten client)

| Endpoint | Purpose |
|---|---|
| `GET /api/labels` | List followed labels |
| `POST /api/labels` | Add a label (create) |
| `GET /api/labels/{id}` | Label detail (artists/releases) |
| `DELETE /api/labels/{id}` | Unfollow label |
| `GET /api/search?type=artist` | Spotify artist search for the add dialog |

This matches `web/src/api/README.md` and will be replaced by the generated
OpenAPI client when the backend contract lands.