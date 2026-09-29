---
type: "Workflow"
title: "Katalog - Release Discovery and Label Verification"
description: "End-to-end workflow of followed-label release discovery: fuzzy label-filtered Spotify search, per-album real-label verification (label field plus copyright parsing), idempotent upsert, self-healing unlinks, the rename-time link audit, and scheduling/failure handling."
tags: [release-discovery, spotify, label-verification, polling, label-albums, resilience, rename-audit]
verified:
  - by: openwiki/0.6.0
    at: 2026-09-29T18:32:14.763Z
sources:
  - id: openwiki-source-da25dbe2acfc73dee3932ad8
    resource: repo://apps/katalog-api/src/Katalog.Api/Api/Endpoints/LabelsEndpoints.cs
  - id: openwiki-source-5eba8b32245b8374e90fe01f
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Labels/CreateLabel.cs
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
  - id: openwiki-source-608cc5c60ce2f34e950e04df
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/SpotifySetup.cs
  - id: openwiki-source-6b4bff950bc16c4ff85b53cc
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Integration/ReleasesPollingIntegrationTests.cs
generated: { by: "openwiki/0.6.0", at: "2026-09-29T18:32:14.763Z" }
---

# Katalog - Release Discovery and Label Verification

This page documents the runtime workflow that discovers releases for followed
labels and guarantees that a release is only ever listed under the label Spotify
itself attributes it to. The core problem: Spotify's `label:"<name>"` search
filter matches **fuzzily** (following "Globuli" also returns albums from
near-miss labels such as "Globulin"), so search alone can never decide
membership. Every candidate is therefore verified against its full album object
before it is stored or linked. The verification pipeline lives in
`ReleasePoller.cs`; scheduling lives in `ReleasesPollingService.cs`; the Spotify
HTTP boundary lives in `SpotifyApiClient.cs` with the resilience pipeline in
`SpotifySetup.cs`.

For the domain rationale (why labels are app-owned, the junction invariant) see
[Domain](../domain/README.md); for quota and operational sizing see
[Operations](../operations/README.md); for system context see
[Architecture](../architecture/README.md).

## Discovery and verification flow

<!-- openwiki: mermaid parse failed and this diagram was converted to a text fence so it does not break rendering. Fix the diagram source and restore the mermaid fence. Parser error: Heuristic: an unescaped angle bracket inside a label breaks rendering; rephrase the label. -->
```text
flowchart TD
    START["PollOnceAsync or PollLabelAsync"] --> SEARCH["SearchAlbumsByLabelAsync<br/>v1/search q=label quoted name<br/>paginated via next URLs"]
    SEARCH --> CAND{"for each<br/>search candidate"}
    CAND --> GET["GetAlbumAsync<br/>GET /albums/id full object"]
    GET --> EXTRACT["ExtractRealLabel<br/>label field, else copyright parsing"]
    EXTRACT -->|no usable label| SKIP["skip candidate<br/>re-verified next cycle"]
    EXTRACT -->|real label found| MATCH{"exact match vs<br/>followed label name<br/>case-insensitive, trimmed"}
    MATCH -->|mismatch| UNLINK["RemoveVerifiedMismatchedLabelAlbumAsync<br/>delete existing link if any"]
    MATCH -->|exact| UPSERT["UpsertAlbumAsync<br/>ON CONFLICT spotify_id<br/>store real label as label_spotify"]
    UPSERT --> LINK["UpsertLabelAlbumAsync<br/>atomic link guarded by label's<br/>committed current name FOR KEY SHARE"]
```

The discovery/verification pipeline: paginated fuzzy label search, one
`GET /albums/{id}` per candidate, real-label extraction, then the
match/mismatch/unverifiable branches.

### Step 1 - label-filtered search

`ReleasePoller.FetchLabelAlbumsAsync` calls
`ISpotifyApiClient.SearchAlbumsByLabelAsync` with the followed label's name, the
configured market (`SpotifyOptions.Market`, default `SE`), the per-request limit
clamped to Spotify's max of 10 (`SpotifyOptions.SearchLimitMax`), and
`maxItems: null` so the **full** result set is paged through - labels with more
than 10 releases are not silently capped. The client builds the query as
`label:"<name>"` (an undocumented-but-working filter, verified live 2026-09-22),
escapes backslashes and quotes in the name, and follows Spotify's `next`
pagination URLs after validating each one is same-origin against the configured
base address with no user-info (`ValidateNextPage`).

### Step 2 - per-candidate verification

Search returns *simplified* album objects that omit the `label` field, so
`VerifyAndUpsertAlbumAsync` issues one `GET /albums/{id}` per candidate
(`SpotifyApiClient.GetAlbumAsync`, which returns `null` on 404) to fetch the
full album object. This one-GET-per-candidate shape is the captain's explicit
quota choice: correctness of label attribution over request count.

### Step 3 - real-label extraction

`ExtractRealLabel` determines the album's real label from the full album object:

1. If the top-level `label` field is present and non-blank, its trimmed value
   wins.
2. Otherwise the `copyrights` array is parsed: each line is matched against
   `CopyrightLabelRegex`, which strips an optional `©`/`℗`/`(C)`/`(P)`/`C`/`P`
   prefix, a leading 4-digit year, and trailing legal text introduced by
   `under` or `a division of` (e.g. `"© 2025 Globuli"` or
   `"2025 Globuli, a division of X"` both yield `Globuli`). The first copyright
   line yielding a non-blank label wins.
3. If neither source yields a label, the candidate is **unverifiable**.

### Step 4 - match / mismatch / unverifiable branches

The extracted real label is compared against the followed label's name with an
exact, case-insensitive, trimmed (`OrdinalIgnoreCase` after `Trim()`) match:

- **Exact match** - the album is upserted and linked (see write paths below).
  The stored `label_spotify` attribution is the album's *real* Spotify label
  (Spotify's own casing), not the discovering label's name.
- **Verified mismatch** - `RemoveVerifiedMismatchedLabelAlbumAsync` deletes any
  existing `label_albums` link between the album and the label. This is the
  **only deletion path** for `label_albums` rows and it is what makes polling
  self-healing: links written before exact verification existed, or invalidated
  later, are removed when the discovery search re-encounters the contaminated
  album. A candidate that search merely stops returning is never deleted.
- **Unverifiable** (album GET failed, 404, or no usable label) - the candidate
  is skipped; nothing is stored or removed, and the next poll cycle re-fetches
  and re-verifies it. Discovery never upserts an unverified candidate.

## Write paths

All poller DB writes are raw SQL on the shared `KatalogContext` connection via
`PrepareCommandAsync`, which binds the active EF transaction when one exists -
so writes performed during the rename-time audit commit or roll back together
with the rename.

- **`UpsertAlbumAsync`** - idempotent album upsert:
  `INSERT INTO albums ... ON CONFLICT (spotify_id) DO UPDATE`, returning the
  album id. `label_spotify` is set to the album's real verified label, so a
  re-verified album's attribution is corrected to what Spotify currently
  reports. It then upserts the album's artists (`ON CONFLICT (spotify_id)`),
  the `album_artists` junction rows with position
  (`ON CONFLICT (album_id, artist_id) DO UPDATE SET position`), and removes
  `album_artists` rows for artists no longer on the album.
- **`UpsertLabelAlbumAsync`** - the link write is a single atomic statement:
  `INSERT INTO label_albums ... SELECT ... FROM labels l WHERE l.id = @labelId
  AND trim(lower(l.name)) = trim(lower(@spotifyLabel)) FOR KEY SHARE OF l
  ON CONFLICT (label_id, album_id) DO UPDATE SET last_confirmed_at_utc = now()`.
  The candidate's already-verified real label must exactly equal the label's
  **committed current name**, re-read under a `FOR KEY SHARE` row lock; a poll
  pass carrying a stale pre-rename snapshot therefore affects zero rows and can
  never link (or resurrect after the audit's delete) an album whose real label
  no longer matches the renamed label. Links here are add/confirm only.
- **`RemoveVerifiedMismatchedLabelAlbumAsync`** - the sole deletion path for
  `label_albums` rows (`DELETE FROM label_albums ... USING albums ... WHERE
  a.spotify_id = @spotifyId`), invoked only after a positively verified
  real-label mismatch (the album GET succeeded and reported a different label)
  or by the rename-time audit for 404 albums.

## Scheduling and failure handling

`ReleasesPollingService` is a `MigrationAwareBackgroundService<KatalogContext>`
that polls **once directly at start** and then on every `PeriodicTimer` tick
(`PollingOptions.Interval`, validated to 6-24 h, default 12 h). Each cycle runs
in its own DI scope; a failing cycle is logged and swallowed so the host keeps
running and the next interval retries.

`ReleasePoller.PollOnceAsync` loads all followed labels, gets-or-creates the
singleton `PollCursor` row, and polls each label **in isolation**: a per-label
failure is logged and the loop continues, so one label cannot kill the cycle.
After the loop the cursor advances unconditionally (`CursorValue = startedAt`,
`Status = "completed"`) - crash-safety comes from the idempotent upserts, not
from the cursor, so re-running the same data is a no-op. The cursor primary key
is `JobName = "artist_new_releases"`, kept from the earlier artist-discography
polling design to avoid a cursor migration; discovery is now label-based.

When a label is created (`CreateLabel.CreateAsync`), after the create
transaction commits, `PollLabelAsync` runs **immediately in its own async
scope** with a fresh `DbContext`, so releases appear right away and a polling
failure cannot corrupt the request's context or roll the label back - the next
scheduled cycle retries.

### Spotify resilience pipeline

All catalog calls go through the typed `ISpotifyApiClient` registered in
`SpotifySetup.AddSpotify` with a custom Polly pipeline
`TotalTimeout (300 s) → Retry (max 3 attempts, exponential backoff from 2 s with
jitter, `ShouldRetryAfterHeader = true` so Spotify 429 `Retry-After` values are
honoured) → CircuitBreaker (30 s break, 30 s sampling, minimum throughput 5,
failure ratio 0.5) → AttemptTimeout (15 s)`. The standard resilience defaults
are deliberately not used: their 30 s total timeout would guillotine retries
waiting out a long `Retry-After`. Transient means 429/5xx/408, transport
exceptions, or timeouts; user-driven cancellation is never retried. The Bearer
token and the single 401-refresh-retry dance are handled by
`SpotifyTokenHandler`, and the `Authorization` header is redacted from logs.

## Rename-time link audit

A label rename deliberately retargets the follow, and the discovery search will
never re-encounter under the new name the albums linked under the old one - so
poll-cycle self-healing alone cannot clean up after a rename.
`UpdateLabel.UpdateAsync` therefore runs
`ReleasePoller.AuditLabelLinksAsync(labelId, newName)` **inside the same
transaction** as the rename, after saving the new name. The audit re-verifies
**every** existing `label_albums` link of the label against the new name using
the same `GET /albums/{id}` + `ExtractRealLabel` verification discovery uses:

- **Exact match** - the album is re-upserted, correcting any stale
  `label_spotify` attribution.
- **Verified mismatch** - the link is removed via
  `RemoveVerifiedMismatchedLabelAlbumAsync`.
- **404 album** - Spotify reports the album no longer exists (a verified
  upstream fact, never an exact-match candidate), so the stale link is removed.
- **Any other unverifiable link** - album GET failure, 5xx/429 after retries,
  cancellation, or an album reporting no usable label - the audit throws
  `LabelLinkAuditIncompleteException` and aborts.

`UpdateLabel` catches `LabelLinkAuditIncompleteException`, lets the transaction
roll back (the label keeps its old name and **all** its links, so nothing
unverified can ever stay listed), and returns
`UpdateLabelStatus.RenameVerificationFailed`, which `LabelsEndpoints` maps to
**503 Service Unavailable** with a problem-details body telling the caller to
retry when Spotify is reachable. If the name did not change, no audit runs.

## Focused tests

`ReleasesPollingIntegrationTests` (Testcontainers Postgres + WireMock Spotify)
covers the workflow end to end:

- idempotent upsert with cursor advance (`PollOnce_UpsertsAlbumsIdempotently_AndCursorAdvances`);
- 429 + `Retry-After` retried successfully by the resilience pipeline;
- per-label failure isolation: a failing label search stores nothing but the
  cursor still advances;
- label search (not the artist discography) is the source of truth;
- pagination followed until exhausted (11 albums across 2 pages, each verified
  via `GET /albums/{id}`);
- the Globuli/Globulin regression: a near-miss real label is excluded even on
  the immediate poll at label creation;
- exact match is case-insensitive/trimmed and stores the real label's casing;
- unverifiable candidates are skipped while the cursor still advances;
- a pre-existing contaminated link is unlinked on verified mismatch while the
  exact-match album's `label_spotify` is corrected.

`SlugAndPollingParsingTests` unit-covers `ParseAlbumType` and `ParseReleaseDate`
(year/month coerced to the first day of the period).
