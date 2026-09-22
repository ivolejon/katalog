---
type: "Reference"
title: "Katalog - Domain"
openwiki_generated: true
verified:
  - by: openwiki/0.5.2
    at: 2026-09-22T17:28:24.726Z
sources:
  - id: openwiki-source-e0c539bee171277d0d387075
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/ArtistsEndpoints.cs
  - id: openwiki-source-da25dbe2acfc73dee3932ad8
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/LabelsEndpoints.cs
  - id: openwiki-source-a4f9914ac26d9e676f4e4647
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/ReleasesEndpoints.cs
  - id: openwiki-source-c4e6278c448892ab6ee9060c
    resource: repo://contracts/katalog-api/openapi.json
  - id: openwiki-source-ca248e99bf5d44b0aa64b70c
    resource: repo://web/src/api/README.md
generated: { by: "opencode", at: "2026-09-22T17:28:24.726Z" }
---

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
   album-metadata derivation remains a future option).
3. A release poller per linked artist in the backend
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

## Implemented API surface

| Endpoint | Purpose |
|---|---|
| `GET /api/labels` | List followed labels |
| `POST /api/labels` | Add a label (create) |
| `GET /api/labels/{id}` | Label detail (artists/releases) |
| `PUT /api/labels/{id}` | Rename a label |
| `DELETE /api/labels/{id}` | Unfollow label |
| `POST /api/labels/{labelId}/artists` | Link an artist to a label |
| `DELETE /api/labels/{labelId}/artists/{artistId}` | Unlink an artist from a label |
| `GET /api/labels/{labelId}/releases` | List releases for a label |
| `GET /api/search?type=artist` | Spotify artist search for the add dialog |

The route definitions are implemented in `apps/katalog-api/src/Katalog.Api` and
the complete contract is committed at `contracts/katalog-api/openapi.json`.
The frontend currently uses a handwritten client for the routes it needs.
