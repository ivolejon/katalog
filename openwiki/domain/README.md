---
type: "Reference"
title: "Katalog - Domain"
description: "The Katalog label-following business model: app-owned labels with artist anchors, Spotify label-search discovery with exact-match verification, the label_albums authoritative link, the rename-time link audit, and the implemented API surface including paged releases."
tags: [katalog, domain, labels, spotify, release-polling, api]
openwiki_generated: true
sources:
  - id: openwiki-source-e0c539bee171277d0d387075
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/ArtistsEndpoints.cs
  - id: openwiki-source-da25dbe2acfc73dee3932ad8
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/LabelsEndpoints.cs
  - id: openwiki-source-a4f9914ac26d9e676f4e4647
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/ReleasesEndpoints.cs
  - id: openwiki-source-3c763c43a1537f0a5607f0a4
    resource: repo://apps/katalog-api/src/Katalog.Api/Contracts/Contracts.cs
  - id: openwiki-source-49687e36602309b75fa99b81
    resource: repo://apps/katalog-api/src/Katalog.Api/Domain/LabelAlbum.cs
  - id: openwiki-source-5acf6e2be2729e358db87f81
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Labels/SearchLabels.cs
  - id: openwiki-source-e7c0b1df5b2e1c1667c8f284
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Labels/UpdateLabel.cs
  - id: openwiki-source-d3294089ae22810e19e27ec1
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/GetLabelReleases.cs
  - id: openwiki-source-58ae3d06ebcb0a21c5b26e22
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/ReleasePoller.cs
  - id: openwiki-source-417579a58fbbf8a47fb8a114
    resource: repo://apps/katalog-api/src/Katalog.Api/Infrastructure/Configurations/PollCursorConfiguration.cs
  - id: openwiki-source-c4e6278c448892ab6ee9060c
    resource: repo://contracts/katalog-api/openapi.json
  - id: openwiki-source-ca248e99bf5d44b0aa64b70c
    resource: repo://web/src/api/README.md
  - id: openwiki-source-9f5b46e5bcdaf05596eb9060
    resource: repo://web/src/api/types.ts
  - id: openwiki-source-7af276dd62867dad418aa665
    resource: repo://web/src/components/labels/AddLabelDialog.vue
  - id: openwiki-source-bcebeed4f761d52b0c3f321d
    resource: repo://web/src/composables/useLabelSearch.ts
generated: { by: "openwiki/0.6.0", at: "2026-09-29T18:57:22.615Z" }
verified:
  - by: openwiki/0.6.0
    at: 2026-09-29T18:57:22.615Z
---

# Katalog - Domain

## The product

Follow record labels ("labels") via Spotify and see their releases. The user
adds a label (name + linked artists), and the app tracks new albums/singles
from those artists.

Decisions recorded with the captain:

- **Single user** - no multi-tenant design, no user accounts.
- **User adds labels manually** - via Spotify label search in the add-label
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
2. Artists linked to that label (manual selection via label search today;
   album-metadata derivation remains a future option).
3. A release poller per followed label in the backend that discovers albums via
   Spotify's `label:"<name>"` album-search filter and upserts them
   idempotently (`ON CONFLICT (spotify_id)`).

Spotify policy constraints that shape the domain: no indefinite storage of
Spotify content (metadata retention/cleanup required), display must link back
to Spotify with attribution, and dev-mode quota (~5 authenticated users / rate
limits with `Retry-After`) bounds polling frequency.

## Exact-match verification and label_albums invariants

Spotify's `label:"..."` search filter matches **fuzzily** (following "Globuli"
also returns albums from near-miss labels such as "Globulin"), so the poller
never trusts a search hit. These invariants protect the release feed:

- **Verify before linking.** Every candidate album is re-fetched as a full
  album object (`GET /albums/{id}` - one GET per candidate, the captain's
  explicit quota choice). Only albums whose real Spotify `label` field
  **exactly (case-insensitive, trimmed) equals the followed label's name** are
  upserted, and the stored `label_spotify` attribution is the album's real
  label, not the discovering label's name.
- **`label_albums` is the authoritative link.** Album-to-label membership is
  modeled by the `label_albums` junction (`LabelId`/`AlbumId`,
  `FirstSeenAtUtc`/`LastConfirmedAtUtc`), independent of the denormalized
  `albums.label_id` column. Release listing reads through this junction only.
- **Verified mismatch self-heals.** If verification positively reports a real
  label that does not match, the poller removes any existing `label_albums`
  link. This is the *only* deletion path during polling: a candidate that
  cannot be verified (album GET fails, no label reported) is skipped, and a
  transient search miss never deletes a link.
- **Linking is guarded against renames.** The junction insert re-reads the
  label's committed current name under a row lock (`FOR KEY SHARE`) inside one
  atomic SQL statement, so a poll pass carrying a pre-rename snapshot can never
  link an album whose real label no longer matches the renamed label.
- **A rename audits all links.** Renaming a label deliberately retargets it,
  so `UpdateLabel` runs `ReleasePoller.AuditLabelLinksAsync` inside the same
  transaction: exact matches are re-upserted (correcting stale `label_spotify`
  attribution), verified mismatches and Spotify-404 albums are unlinked, and
  any link Spotify cannot verify (GET failure, 5xx/429, cancellation, or no
  label reported) throws `LabelLinkAuditIncompleteException`. The exception
  rolls the whole transaction back - the label keeps its old name and all its
  links - and the API returns **503 Service Unavailable** so the rename can be
  retried when Spotify is reachable. Nothing unverified is ever listed.
- **Cursor name kept for compatibility.** `ReleasePoller.JobName` remains
  `'artist_new_releases'` even though polling is now label-search based: the
  constant is the `poll_cursors` primary key, and keeping the name avoids a
  cursor migration.

The poll runs on a configurable rhythm (6-24 h, default 12 h, poll once at
startup then on a `PeriodicTimer`) via `ReleasesPollingService`, and following
a new label triggers an immediate `PollLabelAsync` so releases appear without
waiting for the next cycle.

## Paging domain rule for releases

`GET /api/labels/{labelId}/releases` is dual-mode. With no query parameters it
returns the full release list (backwards compatibility for older clients).
With `page`/`pageSize` it returns a paged `LabelReleasesResponse`:

- `page` and `pageSize` are **1-based and must be positive**; a non-positive
  value yields **400 Bad Request** ("Page and pageSize must be positive."). A
  missing value defaults to `page=1` / `pageSize=20`. An unknown label yields
  **404**.
- Releases sort **newest first**; NULL release dates are coalesced to
  `DateOnly.MinValue` so they sort last under descending order (Postgres
  sorts NULLs first on DESC by default, hence the explicit coalesce).
- `hasMore = page * pageSize < totalCount`, where `totalCount` is the count of
  `label_albums` links for the label. Paging is performed in the database; no
  Spotify calls are made per page.

The frontend's **Releaser** tab (`LabelDetailView`) consumes pages of 5 and
reveals more via a **Ladda mer** button, merging pages and surfacing newly
polled releases as a "new releases available" indicator.

## Current frontend behavior

- `LabelsView`: list of followed labels; add-label dialog with debounced
  Spotify label search (`useLabelSearch`, 350 ms, empties reset without an API
  call) against `GET /api/labels/search`; the dialog derives artist anchors by
  collecting unique Spotify artist ids from the hit's albums
  (`collectArtistIds`).
- `LabelDetailView`: tabs for **Artists** and **Releases**; album cards link
  to Spotify. The Releases tab shows an initial page and reveals more via a
  **Ladda mer** button.
- Empty/error/skeleton states everywhere; stale-response guards in the Pinia
  store so an in-flight refresh cannot overwrite newer local state.
- No login: the app opens straight to the landing page and `/labels`.

## Implemented API surface

| Endpoint | Purpose |
|---|---|
| `GET /api/labels` | List followed labels |
| `POST /api/labels` | Add a label (create; links Spotify artist ids as anchors; 404 unknown artist, 409 slug conflict) |
| `GET /api/labels/search?q=...&limit=...` | Spotify label search proxying the `label:"..."` album filter; returns the searched term as one label hit with matching albums for anchor building |
| `GET /api/labels/{id}?includeReleases=...` | Label detail (artists + release count); `includeReleases=false` omits the release list (defaults to true) |
| `PUT /api/labels/{id}` | Rename a label (audits all links; 404 unknown, 409 slug conflict, 503 when Spotify cannot verify) |
| `DELETE /api/labels/{id}` | Unfollow label (204 / 404) |
| `POST /api/labels/{labelId}/artists` | Link an artist to a label |
| `DELETE /api/labels/{labelId}/artists/{artistId}` | Unlink an artist (409 when it is the label's last artist - a label must keep at least one) |
| `GET /api/labels/{labelId}/releases` | Dual-mode: full list without params, or paged `LabelReleasesResponse` with `page`/`pageSize` (400 non-positive, 404 unknown label) |
| `GET /api/search?q=...&type=artist` | Spotify artist search proxy (`type` must be `artist` when given) |

The route definitions are implemented in `apps/katalog-api/src/Katalog.Api` and
the complete contract - OpenAPI 3.1.1, `"title": "Katalog.Api | v1"`,
`"version": "1.0.0"` - is committed at `contracts/katalog-api/openapi.json`.
The frontend currently uses a handwritten typed client (`web/src/api/types.ts`,
`web/src/api/README.md`) matching that contract; running
`npm run generate:client` (hey-api) emits a generated client under
`web/src/api/generated/` that is intended to replace the handwritten one.
