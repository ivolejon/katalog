---
type: "Reference"
title: "Katalog - Testing"
description: "How Katalog is verified: xUnit v3 unit tests, Testcontainers + WireMock integration tests (label verification, copyright parsing, rename audits), and Aspire AppHost smoke tests."
tags: [katalog, testing, xunit, testcontainers, wiremock, aspire, spotify, ci]
openwiki_generated: true
sources:
  - id: openwiki-source-164e2da859b5277df81c7d94
    resource: repo://.github/workflows/ci.yml
  - id: openwiki-source-d23c93c81dff4d6aec30e9fb
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/AppHost/AppHostSmokeTests.cs
  - id: openwiki-source-3ecdef727ac742bdd7e6c68b
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/AppHost/StablePortTests.cs
  - id: openwiki-source-70b161024c91b0b548181661
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Integration/KatalogApiFactory.cs
  - id: openwiki-source-064c36b36867a42c9247c3ba
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Integration/LabelsApiIntegrationTests.cs
  - id: openwiki-source-58fb4d41923aeff2b04e4567
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Integration/PostgresFixture.cs
  - id: openwiki-source-6b4bff950bc16c4ff85b53cc
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Integration/ReleasesPollingIntegrationTests.cs
  - id: openwiki-source-1162d3cad109ebc313dcd399
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Integration/WireMockSpotify.cs
  - id: openwiki-source-7796feddb7c97ca8d4140109
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Katalog.Api.Tests.csproj
  - id: openwiki-source-b565d9f9aef04f743d6402fd
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Unit/SlugAndPollingParsingTests.cs
  - id: openwiki-source-206c3ecaf2e0cbc3d9d77467
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Unit/SpotifyTokenProviderTests.cs
generated: { by: "openwiki/0.6.0", at: "2026-09-29T18:32:14.763Z" }
verified:
  - by: openwiki/0.6.0
    at: 2026-09-29T18:32:14.763Z
---

# Katalog - Testing

Katalog's behavior is verified in three backend layers plus frontend CI. The
backend test project `apps/katalog-api/tests/Katalog.Api.Tests` covers unit
tests, PostgreSQL + WireMock integration tests, and AppHost smoke tests using
xUnit v3, Testcontainers.PostgreSql, WireMock.Net, and Aspire.Hosting.Testing.
CI (`.github/workflows/ci.yml`) builds and tests the full solution and
separately regenerates the committed OpenAPI contract to fail on contract
drift; `.github/workflows/web.yml` typechecks (`vue-tsc -b`) and builds
(`vite build`) the frontend.

## Test layers

<!-- openwiki: mermaid parse failed and this diagram was converted to a text fence so it does not break rendering. Fix the diagram source and restore the mermaid fence. Parser error: Heuristic: an unescaped angle bracket inside a label breaks rendering; rephrase the label. -->
```text
flowchart TD
    Unit["Unit tests<br/>slug, poll parsing, token provider"]
    Integration["Integration tests<br/>WebApplicationFactory + Testcontainers Postgres + WireMockSpotify"]
    AppHost["AppHost smoke tests<br/>DistributedApplicationTestingBuilder"]
    CI["CI: dotnet test Katalog.slnx<br/>+ web.yml typecheck/build"]
    Unit --> CI
    Integration --> CI
    AppHost --> CI
```

The three backend test layers all run under `dotnet test Katalog.slnx` in CI.

### Unit tests

- `Unit/SlugAndPollingParsingTests.cs` - `LabelSlug.From` normalization
  (diacritics, whitespace, punctuation) and `ReleasePoller` parsing helpers:
  `ParseAlbumType` mapping, `ParseReleaseDate` normalizing month/year
  precision to the first of the period and returning a null date for missing
  values.
- `Unit/SpotifyTokenProviderTests.cs` - the client-credentials token cache
  with `FakeTimeProvider`: first call fetches and caches, expiry triggers a
  fresh fetch, `ForceRefreshAsync` invalidates the cache, and concurrent
  callers share a single token request.

### Integration tests

Integration tests run the real API through
`WebApplicationFactory<Katalog.Api.KatalogApiMarker>` against a
Testcontainers PostgreSQL container and a WireMock Spotify stub
(`Integration/KatalogApiFactory.cs`, `Integration/PostgresFixture.cs`,
`Integration/WireMockSpotify.cs`).

`KatalogApiFactory` wires the app under test to the fixtures by overriding
configuration: `ConnectionStrings:catalog` points at the container's
`catalog` database, `Spotify:BaseUrl`/`Spotify:AccountsBaseUrl` point at the
WireMock server, and placeholder client credentials win over real user
secrets through the configuration hierarchy, so tests never touch the real
Spotify API. Two safeguards keep runs deterministic:

- `Polling:Interval` is set to `12:00:00` (12 hours, inside the app's
  validated 6-24 h range - `TimeSpan.Parse("24:00:00")` would mean 24
  *days*), so the hosted poller's timer never fires mid-test.
- All `IHostedService` registrations are removed, so the release polling
  background service never races the tests; the suite instead resolves
  `ReleasePoller` from a scope and calls `PollOnceAsync` itself.

`PostgresFixture` starts one `postgres:18.3` container per test class,
creates the `catalog` database, and `KatalogApiFactory.ResetDatabaseAsync`
truncates all tables (`album_artists`, `label_albums`, `artist_label`,
`albums`, `artists`, `labels`, `poll_cursors`) so each test starts from an
empty migrated schema. `WireMockSpotify.Reset()` clears mappings, scenarios,
and request logs between tests; WireMock scenarios are global per server
instance, so the 429 test mints a fresh scenario name per run.

#### WireMockSpotify and the two real-label shapes

`WireMockSpotify` stubs the Spotify endpoints Katalog calls: the token
exchange (`StubTokenExchange`), artist GETs, artist album lists
(`StubArtistAlbums`), and the label-filtered album search (`StubLabelSearch`,
`StubLabelSearchPage` - matching `q=label:"<name>"`, `type=album`,
`market=SE`, `limit=10`, plus `offset` for pages).

Release discovery verifies every search candidate's **real** label via
`GET /v1/albums/{id}` before linking it, and Spotify returns that label in
two shapes - the page-specific helpers stub both:

- `StubAlbumGet(albumId, label, ...)` - the full album object carrying the
  top-level `label` field (the classic shape).
- `StubAlbumGetByCopyright(albumId, label, ..., copyrightSuffix)` - the
  copyrights-only shape Spotify currently returns for many albums (e.g.
  "2025 Globuli"): no top-level `label`, the real label embedded in the
  `copyrights` array text (`{"text": "<year> <label>", "type": "P"}`). The
  optional `copyrightSuffix` appends trailing legal text such as
  " under exclusive license to Sony Music" to the P-line.

#### LabelsApiIntegrationTests

End-to-end API coverage against the factory:

- **CRUD and validation** - label create/list/detail/update/delete,
  duplicate-slug 409, invalid name 400, unique/non-empty artist ids, artist
  linking (idempotent re-link, 404 for an artist unknown to Spotify).
- **Label search proxying** - `/api/labels/search` forwards the
  `label:"<name>"` filter to Spotify with quotes surviving URI encoding,
  escapes quotes embedded in the label name (`ACME "Records"` ->
  `label:"ACME \"Records\""`), stops paging once `limit` items are collected
  (a 500 on page 2 proves bounded pagination), and rejects `limit` above
  Spotify's cap of 10 with 400 before forwarding.
- **Immediate discovery on create** - creating a label synchronously
  discovers its releases (no empty "No releases yet" state) without
  advancing the scheduled poll cursor; discovery failure still creates the
  label with zero releases; an unknown Spotify artist id rolls the whole
  create back with 404. Copyright-parsing regressions:
  `CreateLabel_DiscoversReleasesImmediately_WhenSpotifyOmitsAlbumLabelField`
  (label only in `copyrights`) and
  `..._WhenCopyrightContainsTrailingLegalText` (extraction stops at
  "under exclusive license to ...").
- **Fuzzy-hit exclusion** - Spotify's label filter matches fuzzily, so a
  search for label B can return an album whose real label is label A; exact
  real-label verification keeps it linked only under label A.
- **Rename audit** (`PUT /api/labels/{id}`) - renaming a label audits every
  existing link once against the new name: verified mismatches are unlinked,
  exact matches keep their link and get `labelSpotify` corrected to the
  album's real label; a 404 album GET is a verified upstream fact and the
  album is unlinked while the rename succeeds; if Spotify cannot verify a
  link the rename fails closed with 503 ("The label was not renamed."), the
  transaction rolls back, and the old name and links stay intact; a no-op
  rename (normalized name unchanged) skips the audit entirely and succeeds
  even when Spotify is unreachable, consuming no album-GET quota.
- **Stale pre-rename poll pass guard** - a poll pass that snapshots the
  pre-rename name and runs concurrently with a rename is blocked on the
  rename transaction's row lock and re-verifies against the committed
  current name, so it cannot link or resurrect an album whose real label is
  the old name.

#### ReleasesPollingIntegrationTests

Scheduled-poll coverage driving `ReleasePoller.PollOnceAsync` directly:

- **Idempotent upsert** - repeated polls keep the same album rows and ids,
  and the cursor row for `ReleasePoller.JobName` advances to `completed`.
- **429 / Retry-After** - the custom resilience pipeline honors
  `Retry-After: 1` and retries the label search (WireMock scenario state
  flips from 429 to 200).
- **Per-label failure isolation** - a label search that fails after all
  retries stores no albums for that label, but the cycle completes and the
  cursor still advances.
- **Pagination** - discovery follows the search `next` pointer until
  exhausted, so labels with more than 10 releases are not silently capped.
- **Verification semantics** - near-miss real labels ("Test Label" vs
  "Test Labels") are excluded; an exact case-insensitive, trimmed match is
  included and stored with the album's *real* Spotify label casing;
  unverifiable candidates (album GET 404 or label-less album) are skipped
  while the cursor still advances.
- **Self-healing unlink** - a `label_albums` link created before exact
  verification existed is removed on the next poll once the album's real
  label is a verified mismatch, while the exact-match album keeps its link
  and its `label_spotify` attribution is corrected.

### AppHost smoke tests

`AppHost/AppHostSmokeTests.cs` builds the distributed application model with
`DistributedApplicationTestingBuilder.CreateAsync<Projects.Katalog_AppHost>()`
using placeholder Spotify credentials (the API validates options on start;
the poller fails gracefully), asserts the `postgres`, `catalog`, and `api`
resources exist, asserts the catalog database exposes the "Reset Database"
dashboard command (`reset-db`), starts the stack, and verifies the API's
`/alive` endpoint responds 200.

`AppHost/StablePortTests.cs` pins the stable-port contract: the AppHost
model fixes the api endpoint at 5192 and web at 5173, and the committed
launch profile pins the dashboard to `https://localhost:15000`.

## Frontend verification

- **Typecheck + build** - `vue-tsc -b` and `vite build` pass in CI
  (`.github/workflows/web.yml`) and locally.
- **Live smoke (best effort)** - the initial frontend implementation was
  verified against a mock API in headless Chromium: the app serves, labels
  list/detail and add-label-by-artist-search render, and the release tabs
  show data.
- **Planned frontend scenarios** - no browser test files currently record
  live verification for these scenarios. They are intended for a future
  integration phase against the real backend stack: add label via Spotify
  artist search; dialog state reset on cancel/reopen; debounce (stale search
  results do not repopulate an emptied query); fast navigation between label
  details; concurrent mutation vs refresh.
- The store's stale-response guards (`fetchId`/`revision`) and the debounced
  search composable are deliberately structured so those scenarios can be
  asserted with minimal test infrastructure.

## Conventions and rules

- Naming convention `Method_Condition_Expected` with display names.
- Interactive/e2e smoke against the full stack (Aspire + Postgres + WireMock
  or real Spotify) complements these automated tests and the frontend
  scenarios.
- Never run poll tests against the real Spotify API; always stub/mock. The
  factory enforces this by overriding Spotify base URLs and credentials so
  user secrets are never used.
- Never answer no-mistakes ask-user findings yourself - escalate to
  firstmate, and never pass `--yes` to the gate.
