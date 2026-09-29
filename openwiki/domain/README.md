---
type: "Reference"
title: "Katalog - Domain"
description: "Katalog's label-following business model: app-owned labels, Spotify Web API constraints, exact real-label verification, the label_albums junction invariant, and the implemented API surface."
tags: [katalog, domain, spotify, labels, release-polling, api]
openwiki_generated: true
sources:
  - id: openwiki-source-e0c539bee171277d0d387075
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/ArtistsEndpoints.cs
  - id: openwiki-source-da25dbe2acfc73dee3932ad8
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/LabelsEndpoints.cs
  - id: openwiki-source-a4f9914ac26d9e676f4e4647
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/ReleasesEndpoints.cs
  - id: openwiki-source-126d98f1667a78c6b163976a
    resource: repo://apps/katalog-api/src/Katalog.Api/Domain/Album.cs
  - id: openwiki-source-8ac64c98631f8ac0b4db1acb
    resource: repo://apps/katalog-api/src/Katalog.Api/Domain/Label.cs
  - id: openwiki-source-49687e36602309b75fa99b81
    resource: repo://apps/katalog-api/src/Katalog.Api/Domain/LabelAlbum.cs
  - id: openwiki-source-5eba8b32245b8374e90fe01f
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Labels/CreateLabel.cs
  - id: openwiki-source-5acf6e2be2729e358db87f81
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Labels/SearchLabels.cs
  - id: openwiki-source-e7c0b1df5b2e1c1667c8f284
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Labels/UpdateLabel.cs
  - id: openwiki-source-d3294089ae22810e19e27ec1
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/GetLabelReleases.cs
  - id: openwiki-source-0adb98ff56b305f1f0159c27
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/LabelLinkAuditIncompleteException.cs
  - id: openwiki-source-58ae3d06ebcb0a21c5b26e22
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/ReleasePoller.cs
  - id: openwiki-source-0e9cacfbaf4c025a152cc799
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/ReleasesPollingService.cs
  - id: openwiki-source-bf4ce253af065f2bf88b51c6
    resource: repo://apps/katalog-api/src/Katalog.Api/Infrastructure/Spotify/SpotifyApiClient.cs
  - id: openwiki-source-189a20d60246dfdfb96a4668
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/PollingOptions.cs
  - id: openwiki-source-c4e6278c448892ab6ee9060c
    resource: repo://contracts/katalog-api/openapi.json
  - id: openwiki-source-ca248e99bf5d44b0aa64b70c
    resource: repo://web/src/api/README.md
  - id: openwiki-source-bcebeed4f761d52b0c3f321d
    resource: repo://web/src/composables/useLabelSearch.ts
generated: { by: "openwiki/0.6.0", at: "2026-09-29T18:32:14.763Z" }
verified:
  - by: openwiki/0.6.0
    at: 2026-09-29T18:32:14.763Z
---

# Katalog - Domain

## The product

Follow record labels ("labels") via Spotify and see their releases. The user
adds a label (name + linked artists), and the app discovers and tracks the
albums/singles that Spotify attributes to that label.

Decisions recorded with the captain:

- **Single user** - no multi-tenant design, no user accounts.
- **User adds labels manually** - via the Spotify label search in the
  add-label dialog; the label entity is app-owned.
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
2. Discovery: Spotify's undocumented-but-working `label:"<name>"` album search
   filter (verified live 2026-09-22; not in the official spec's filter list).
   The filter matches **fuzzily** - searching "Globuli" also returns albums
   from near-miss labels such as "Globulin" - so discovery alone can never
   decide membership.
3. Exact verification: each candidate album is re-fetched as a full album
   object (`GET /albums/{id}`, one GET per candidate - the captain's explicit
   quota choice). The album's **real label** is read from the `label` field
   or, when that field is absent, parsed from the `copyrights` array (e.g.
   "© 2025 Globuli" → "Globuli"). Only albums whose real label exactly equals
   the followed label's name (case-insensitive, trimmed) are linked.
4. A release poller **per followed label** in the backend
   (`ReleasePoller.PollOnceAsync` iterates `Labels`, not artists), with an
   idempotent upsert on Spotify album id (`ON CONFLICT (spotify_id)`).

The artists linked at add-label time remain as **anchors**: they seed the
label from the search hits' album artists and give the label detail page its
artist roster, but they no longer drive release discovery - the poller
discovers by label name, not by artist discography.

Spotify policy constraints that shape the domain: no indefinite storage of
Spotify content (metadata retention/cleanup required), display must link back
to Spotify with attribution, and dev-mode quota (~5 authenticated users / rate
limits with `Retry-After`) bounds polling frequency - the poll interval is
configured for a 6-24 h rhythm (default 12 h).

## Data model: label_albums is the source of truth

The domain entities live in `apps/katalog-api/src/Katalog.Api/Domain`:

- `Label` - the app-owned followed label (name, unique slug, timestamps).
- `Album` - a Spotify release. `LabelSpotify` stores the album's **real,
  verified Spotify label** as the release attribution (what the UI shows);
  `LabelId` is a denormalized pointer to the app-owned label the album was
  last discovered/verified for.
- `LabelAlbum` - the many-to-many junction between labels and albums, with
  `FirstSeenAtUtc` / `LastConfirmedAtUtc` timestamps.
- `LabelArtist` - the many-to-many junction between labels and artists, with
  a `Provenance` (manual today).

**`label_albums` is the source of truth for which releases belong to a
label** - release listing (`GetLabelReleases`) queries the junction, never
`album.label_id`. The denormalized `album.label_id` is only a hint about the
last discovering label and plays no role in listing.

The core invariant, enforced at write time and healed at negative verification
time:

> A release is only listed under a label whose name exactly matches the
> album's verified real Spotify label.

Enforcement points (all in `ReleasePoller`):

- **Linking** (`UpsertLabelAlbumAsync`): a single atomic SQL statement inserts
  the junction row only when the label's committed current name, re-read under
  a `FOR KEY SHARE` row lock, exactly equals the candidate's already-verified
  real label (`trim(lower(...))` on both sides). A poll pass carrying a stale
  pre-rename snapshot therefore cannot link - or resurrect - an album whose
  real label no longer matches.
- **Self-heal on verified mismatch** (`RemoveVerifiedMismatchedLabelAlbumAsync`):
  when a poll cycle positively verifies that an album's real label is not the
  followed label, any existing junction link is deleted. This is the *only*
  deletion path for `label_albums` rows: a candidate that cannot be verified,
  or that search simply stops returning, never causes a deletion, so a
  transient Spotify failure can never strip legitimately discovered releases.
- **Unverifiable candidates are skipped**, not linked and not unlinked; the
  next poll cycle re-fetches and re-verifies them.
- The stored `label_spotify` attribution is always the album's real Spotify
  label, never the discovering label's name.

## Rename semantics: audit-or-rollback

Renaming a label (`PUT /api/labels/{id}`) deliberately **retargets** the
follow: the label keeps its artists, but its releases must now match the new
name. Because the discovery search will never re-encounter the old links under
the new name, the rename runs `ReleasePoller.AuditLabelLinksAsync` inside the
same transaction:

- Exact real-label match → link kept, attribution corrected.
- Positively verified mismatch → link removed.
- Album 404 (gone from Spotify's catalog) → stale link removed (a verified
  upstream fact, never an exact-match candidate).
- Any other unverifiable link (album GET failure, 5xx/429, cancellation, or
  an album reporting no usable label) → `LabelLinkAuditIncompleteException`,
  the whole transaction rolls back, and the API returns **503**
  (`RenameVerificationFailed`). The label keeps its old name and all its
  releases, so nothing unverified can ever be listed; the client can retry
  when Spotify is reachable.

A no-op rename (name unchanged) skips the audit entirely and succeeds even
when Spotify is unreachable.

## Release polling lifecycle

`ReleasesPollingService` (a hosted background service that waits for
migrations) polls once at startup and then on every `PeriodicTimer` tick
(`PollingOptions.Interval`, 6-24 h, default 12 h). Each cycle:

1. Loads all followed labels and polls each one independently - a single
   label's failure is logged and skipped, never kills the cycle.
2. For each label, pages through the full `label:"<name>"` search result set
   (Spotify caps search `limit` at 10; pagination follows validated same-origin
   `next` URLs).
3. Verifies and upserts each candidate as above, also upserting the album's
   artists into `album_artists` (position-preserving) and pruning artist links
   Spotify no longer reports.
4. Advances a singleton `PollCursor` row (job name `artist_new_releases` - a
   historical name kept to avoid a cursor migration; the poller now discovers
   by label, not by artist). Re-running the same data is a no-op update, so
   polling is crash-safe.

Creating a label also triggers an immediate `PollLabelAsync` in its own DI
scope so releases appear right away; a polling failure there is logged but
never rolls the label creation back.

## Current frontend behavior

- `LabelsView`: list of followed labels; add-label dialog backed by the
  debounced Spotify **label** search (`useLabelSearch`, 350 ms, empties reset
  without an API call); `collectArtistIds` builds the artist anchors from the
  album hits of the chosen label hit. Follow/unfollow toggles.
- `LabelDetailView`: tabs for **Artists** and **Releases**; album cards link
  to Spotify. The Releases tab shows an initial batch and reveals more via a
  **Ladda mer** button.
- Empty/error/skeleton states everywhere; stale-response guards in the Pinia
  store so an in-flight refresh cannot overwrite newer local state.
- No login: the app opens straight to the landing page and `/labels`.

## Implemented API surface

| Endpoint | Purpose |
|---|---|
| `GET /api/labels/search` | Spotify label search via the `label:"<name>"` album filter (primary add-label flow); 400 on invalid query/limit |
| `GET /api/labels` | List followed labels |
| `POST /api/labels` | Add a label (create + link artist anchors + immediate release poll); 404 unknown artist, 409 slug conflict |
| `GET /api/labels/{id}` | Label detail (artists/releases) |
| `PUT /api/labels/{id}` | Rename a label with rename-time link audit; 404, 409 slug conflict, **503 when Spotify verification fails** (rename rolled back) |
| `DELETE /api/labels/{id}` | Unfollow label |
| `POST /api/labels/{labelId}/artists` | Link an artist to a label |
| `DELETE /api/labels/{labelId}/artists/{artistId}` | Unlink an artist from a label; 409 when it is the label's last artist |
| `GET /api/labels/{labelId}/releases` | List releases for a label (from `label_albums`, newest first) |
| `GET /api/search?type=artist` | Spotify artist search |

The route definitions are implemented in `apps/katalog-api/src/Katalog.Api`
(`LabelsEndpoints`, `ReleasesEndpoints`, `ArtistsEndpoints`) and the complete
contract is committed at `contracts/katalog-api/openapi.json` (OpenAPI 3.1.1,
`Katalog.Api | v1`). The frontend currently uses a handwritten typed client
(`web/src/api/`) matching the contract's endpoint surface; running
`npm run generate:client` regenerates a client from the committed contract.

## Focused tests

- `LabelsApiIntegrationTests` - CRUD end-to-end, label search proxying
  (including quote escaping in the `label:"..."` filter and limit validation),
  immediate discovery on create (incl. `label`-field-absent and
  copyrights-parsing cases), fuzzy-hit exclusion, rename audit
  (unlink verified mismatches, keep exact matches, 503 rollback on
  verification failure, 404-album unlink, no-op rename skipping the audit),
  and the stale-pre-rename poll-pass guard.
- `ReleasesPollingIntegrationTests` - idempotent upsert and cursor advance,
  429/`Retry-After` resilience, per-label failure isolation, pagination,
  near-miss exclusion, case-insensitive exact match storing the real label,
  unverifiable-candidate skip, and self-heal of pre-existing contaminated
  links.
- `SlugAndPollingParsingTests` (unit) - slug derivation, album-type and
  release-date precision parsing.
