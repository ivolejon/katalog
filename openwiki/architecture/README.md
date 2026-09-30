---
type: "Reference"
title: "Katalog - Architecture"
description: "System overview of Katalog: how the Vue SPA, .NET minimal API, PostgreSQL catalog database, Aspire AppHost orchestration, and the Spotify client-credentials integration fit together, including label-search release discovery and the label_albums persistence model."
tags: [architecture, aspire, dotnet, vue, postgresql, spotify, openapi, release-polling]
openwiki_generated: true
verified:
  - by: openwiki/0.6.0
    at: 2026-09-29T18:32:14.763Z
sources:
  - id: openwiki-source-e7c0b1df5b2e1c1667c8f284
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Labels/UpdateLabel.cs
  - id: openwiki-source-d3294089ae22810e19e27ec1
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/GetLabelReleases.cs
  - id: openwiki-source-9629941b13f4fafa96abeb48
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/MigrationAwareBackgroundService.cs
  - id: openwiki-source-58ae3d06ebcb0a21c5b26e22
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/ReleasePoller.cs
  - id: openwiki-source-0e9cacfbaf4c025a152cc799
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/ReleasesPollingService.cs
  - id: openwiki-source-de97ba8b5cbcf932f7a69e1f
    resource: repo://apps/katalog-api/src/Katalog.Api/Infrastructure/Migrations/20260928233935_AddLabelAlbumsJunction.cs
  - id: openwiki-source-55efad680bc588e00d3fa736
    resource: repo://apps/katalog-api/src/Katalog.Api/Infrastructure/OpenApiDocumentGeneration.cs
  - id: openwiki-source-40176359058e84debec9e8ac
    resource: repo://apps/katalog-api/src/Katalog.Api/Program.cs
  - id: openwiki-source-c6439792c740248aab266e58
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/FeaturesSetup.cs
  - id: openwiki-source-189a20d60246dfdfb96a4668
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/PollingOptions.cs
  - id: openwiki-source-ede7bd6e267ae34cc7d2e28d
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/SpotifyOptions.cs
  - id: openwiki-source-608cc5c60ce2f34e950e04df
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/SpotifySetup.cs
  - id: openwiki-source-c4e6278c448892ab6ee9060c
    resource: repo://contracts/katalog-api/openapi.json
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
  - id: openwiki-source-02df65b0d854782ae484d583
    resource: repo://Katalog.AppHost/Properties/launchSettings.json
  - id: openwiki-source-4fd82268b1f7ce8f04d0e00c
    resource: repo://Katalog.AppHost/Resources/Api/KatalogApi.cs
  - id: openwiki-source-8959a04ec0f63cd2b5aa0073
    resource: repo://Katalog.AppHost/Resources/Infrastructure/PostgresResourceBuilderExtensions.cs
  - id: openwiki-source-24769c136f36ae848d9cb089
    resource: repo://web/src/api/labels.ts
  - id: openwiki-source-7af276dd62867dad418aa665
    resource: repo://web/src/components/labels/AddLabelDialog.vue
  - id: openwiki-source-bcebeed4f761d52b0c3f321d
    resource: repo://web/src/composables/useLabelSearch.ts
generated: { by: "openwiki/0.6.0", at: "2026-09-29T18:32:14.763Z" }
---

# Katalog - Architecture

## System overview

<!-- openwiki: mermaid parse failed and this diagram was converted to a text fence so it does not break rendering. Fix the diagram source and restore the mermaid fence. Parser error: Heuristic: an unescaped angle bracket inside a label breaks rendering; rephrase the label. -->
```text
flowchart TD
    subgraph Aspire["Katalog.AppHost (Aspire orchestration)"]
        WEB["web - Vue 3 SPA<br/>Vite dev server, port 5173"]
        API["api - Katalog.Api<br/>.NET 10 minimal API, port 5192"]
        PG[("PostgreSQL<br/>catalog database")]
    end
    SPOTIFY["Spotify Web API<br/>client-credentials app token"]

    WEB -->|"relative /api/* (Vite dev proxy)"| API
    API -->|"EF Core 10 + Npgsql<br/>connection name catalog"| PG
    API -->|"label: search + GET /albums/id<br/>typed client + resilience pipeline"| SPOTIFY
```

The checked-in backend is a .NET 10 minimal API hosted by `Katalog.Api`.
`Katalog.AppHost` starts it with PostgreSQL and the Vue app through Aspire;
`contracts/katalog-api/openapi.json` is the committed build-generated contract.
No user OAuth and no app auth are used in the MVP - all Spotify data comes from
a client-credentials app token.

## Orchestration (`Katalog.AppHost`)

`Katalog.AppHost/Program.cs` wires the whole local system:

- `builder.SetupPostgres()` provides a single Postgres server with one
  application database; `postgres.AddDatabase("catalog").WithResetCommand()`
  adds the `catalog` database and a **"Reset Database"** dashboard action
  (`reset-db`) that connects to the `postgres` maintenance database, runs
  `DROP DATABASE ... WITH (FORCE)` followed by a plain `CREATE DATABASE`, asks
  for confirmation, and is enabled only while the resource is healthy
  (`PostgresResourceBuilderExtensions.cs`).
- `builder.SetupKatalogApi(catalogDb)` adds the `Katalog.Api` project,
  references and waits for the database, forces the **Development**
  environment (`ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`) so Spotify
  user-secrets load and startup migrations stay enabled (the default
  Production environment skips both and options validation fails the host),
  pins a fixed host port **5192** (`WithHttpEndpoint`), and registers an
  `/alive` HTTP health check so the API does not report "Running" before
  Postgres is up and startup migrations have run.
- `builder.AddViteApp("web", "../web", runScriptName: "dev")` adds the Vue SPA
  with a fixed host port **5173** (matching Vite's own default so
  `vite --port 5173` serves the frontend where the docs say it lives),
  references the API, injects `API_HTTP` from the API endpoint, exposes
  external endpoints, and `WaitFor(api)`s before starting.

A comment block in `Program.cs` documents the three stable-port owners so
every `aspire run` uses the same URLs: the **dashboard/AppHost** is pinned to
`https://localhost:15000` via the `applicationUrl` in
`Katalog.AppHost/Properties/launchSettings.json` (the launch profile is the
mechanism the Aspire CLI 13.5.x uses to pin the dashboard port - without it
the CLI assigns a random port per run), the **api** owns fixed host port 5192
in `Resources/Api/KatalogApi.cs`, and the **web** resource owns fixed host
port 5173 in `Program.cs`.

## Frontend (`web/`)

Vue 3.5 + TypeScript + Vite, styled with the shadcn-vue **a1AhVxI** preset
(reka-ui base, "maia" style, Hugeicons icons, Figtree font, neutral base
color, Tailwind CSS 4 via `@tailwindcss/vite`). CSS-first theming lives in
`web/src/assets/index.css` (variables + `@theme inline`).

Key structure:

- `src/views/` - route components: `HomeView`, `LabelsView` (overview),
  `LabelDetailView` (artist/release tabs).
- `src/router.ts` - lazy routes `/`, `/labels`, `/labels/:id`; sets `title`.
- `src/stores/labels.ts` - Pinia store for labels: fetch with stale-response
  guards (`fetchId`/`revision`), follow/unfollow, mutation vs refresh
  protection.
- `src/composables/useLabelSearch.ts` - debounced Spotify label search
  (GET `/api/labels/search`) backing the add-label dialog; empty queries
  reset without hitting the API, and a request-sequence guard drops stale
  responses. `collectArtistIds()` derives the unique Spotify artist ids from
  a label hit's albums.
- `src/components/labels/AddLabelDialog.vue` - the primary add-label flow:
  the user searches Spotify for a label via `useLabelSearch`, picks a hit,
  and the dialog submits the label name plus the artist ids collected from
  its albums to `POST /api/labels`. It replaces the earlier
  `useArtistSearch`-based combobox flow.
- `src/api/` - **handwritten typed client** today (`http.ts`, `types.ts`,
  `labels.ts`; `API_CLIENT_ORIGIN = 'handwritten'`); the committed
  `contracts/katalog-api/openapi.json` is available when switching to the
  generated `@hey-api/openapi-ts` client (`npm run generate:client`). Endpoint
  map: see `src/api/README.md`.
- `src/components/` - feature components (`labels/`, `albums/`, `artists/`,
  `common/`, `layout/`) plus generated shadcn-vue primitives in
  `src/components/ui/`.
- `src/lib/icons.ts` - bridge for `@hugeicons/vue` imports shadcn-vue emits
  but the package does not export (extend when re-adding components).

Dev proxy (`vite.config.ts`): SPA always calls relative `/api/*`; Vite proxies
to `API_HTTP`/`API_HTTPS` when injected (Aspire resource) with a
`http://localhost:5192` fallback.

## Backend (`apps/katalog-api/`)

`Katalog.Api` is a .NET 10 minimal API with vertical slices, an in-process
release-polling `BackgroundService`, EF Core 10 + Npgsql against one
`catalog` Postgres database, migrations via `--migrate` in the same binary, and
the committed OpenAPI contract in `contracts/katalog-api/openapi.json`.
Aspire orchestration is provided by `Katalog.AppHost`.
Source of truth: `data/katalog-arch-ref-q1/report.md` (external to this repo).

`Program.cs` has two modes: a migration CLI (`--migrate` / `--rollback
<Migration>`) that builds a minimal host and exits, and the normal web host.
Startup registers service defaults (OpenTelemetry, health checks, service
discovery, resilience), problem details, validated options, the EF Core
context via Aspire's `AddNpgsqlDbContext<KatalogContext>("catalog")` with the
snake_case naming convention, the Spotify integration, and the vertical-slice
features. It then applies startup migrations outside deployment environments
and maps the endpoint groups: `MapLabelsEndpoints` (`/api/labels`, including
`/api/labels/search`), `MapArtistsEndpoints` (`/api/search?type=artist`), and
`MapReleasesEndpoints` (`/api/labels/{labelId}/releases`), plus `MapOpenApi`.
All of these paths appear in the committed
`contracts/katalog-api/openapi.json`, which is generated at build time by the
`GetDocument.Insider` host; `OpenApiDocumentGeneration.IsActive` guards every
setup that touches external systems (live database, Spotify, options
validation, the polling worker) so the project builds without them.

### Spotify integration

`SpotifySetup.AddSpotify` registers a singleton `SpotifyTokenProvider`
(client-credentials token, cached and serialized), a `SpotifyTokenHandler`
delegating handler that attaches the Bearer token (with one 401-refresh-retry
dance), and a typed `ISpotifyApiClient` with a custom Polly resilience
pipeline `TotalTimeout (300 s) → Retry (3 attempts, exponential, honours
Retry-After on 429) → CircuitBreaker → AttemptTimeout (15 s)`. Standard
resilience defaults are deliberately not used: Spotify 429s can carry a
`Retry-After` longer than the default total timeout, which would guillotine
the retries. Credentials come from user secrets or environment variables
(`Spotify:ClientId`/`ClientSecret`), never from committed appsettings.

### Release discovery: label-search polling with per-candidate verification

Release discovery is **label-search polling**, not a poller per linked
artist. `ReleasesPollingService` (a `MigrationAwareBackgroundService` that
idles until no EF migrations are pending, so HTTP serving is never blocked)
polls once at startup and then on a `PeriodicTimer` at the configured
`Polling:Interval` (6-24 h, default 12 h). Each cycle, `ReleasePoller` runs
Spotify's `label:"<name>"` album search filter for every followed label and
processes each candidate:

<!-- openwiki: mermaid parse failed and this diagram was converted to a text fence so it does not break rendering. Fix the diagram source and restore the mermaid fence. Parser error: Heuristic: a semicolon inside a label breaks rendering; rephrase the label. -->
```text
flowchart TD
    A["ReleasesPollingService tick<br/>(startup + every Polling:Interval)"] --> B["ReleasePoller.PollOnceAsync<br/>for each followed label"]
    B --> C["Spotify search<br/>label:&quot;name&quot; type=album, paged"]
    C --> D["Verify candidate<br/>GET /albums/id (one GET per candidate)"]
    D --> E{"Real label exactly equals<br/>label's current name?"}
    E -->|"yes"| F["Upsert album ON CONFLICT (spotify_id)<br/>+ label_albums link + album_artists"]
    E -->|"verified mismatch"| G["Delete existing label_albums link<br/>(self-heal)"]
    E -->|"unverifiable (GET failed, no label)"| H["Skip; next cycle re-verifies"]
    F --> I["PollCursor row records<br/>last successful run"]
```

Because Spotify's `label:` filter matches fuzzily (following "Globuli" also
returns near-miss labels like "Globulin"), every candidate is verified
against its full album object (GET `/albums/{id}` - one GET per candidate, an
explicit quota choice): only albums whose real Spotify label (the `label`
field, or a normalized `copyrights` line when the field is absent) exactly
equals the followed label's name (case-insensitive, trimmed) are upserted.
The stored `label_spotify` attribution is the album's real Spotify label, not
the discovering label's name. A positively verified mismatch **removes** any
existing junction link (self-heal), so a release never stays listed under a
label whose real label is not exactly the followed one; an unverifiable
candidate is skipped and retried next cycle. Following a new label triggers
an immediate `PollLabelAsync` so releases appear without waiting for the next
cycle, and a polling failure there never rolls the label creation back.

Upserts are idempotent raw SQL on the shared EF connection/transaction:
`INSERT ... ON CONFLICT (spotify_id) DO UPDATE` for albums and artists, and a
single atomic `INSERT ... SELECT ... FROM labels WHERE trim(lower(name)) =
trim(lower(@spotifyLabel)) FOR KEY SHARE` for the `label_albums` link, which
re-reads the label's committed current name under a row lock so a poll pass
carrying a pre-rename snapshot can never link (or resurrect) an album whose
real label no longer matches. A singleton `PollCursor` row (job name
`artist_new_releases`, kept from the pre-label-search era to avoid a cursor
migration) records the last successful run, making crashes safe to resume.

Renaming a label (`UpdateLabel`) deliberately retargets it, so the rename
runs `ReleasePoller.AuditLabelLinksAsync` inside the same transaction: every
existing link is re-verified via GET `/albums/{id}`, verified mismatches and
Spotify-404 albums are unlinked, exact matches are re-upserted with corrected
attribution, and any unverifiable link aborts the audit with
`LabelLinkAuditIncompleteException`, rolling the whole rename back (HTTP 503)
so nothing unverified stays listed.

### Persistence model

`KatalogContext` exposes `Labels`, `Artists`, `Albums`, and the junction sets
`LabelArtists`, `LabelAlbums`, `AlbumArtists`, plus `PollCursors`, with
snake_case naming, no JSON columns, and enums as ints. The `label_albums`
junction (migration `AddLabelAlbumsJunction`, composite PK
`(label_id, album_id)`, cascade deletes to both parents) models the
label-release relationship independently of the denormalized
`albums.label_id`: it carries `first_seen_at_utc` /
`last_confirmed_at_utc`, and `GET /api/labels/{labelId}/releases` reads
releases exclusively through it (newest first, NULL dates last). The only
deletion paths for `label_albums` rows are a positively verified real-label
mismatch and the rename-time audit - a transient search miss never deletes a
legitimately discovered album.

## Frontend/backend coupling decisions

- Relative `/api` calls from the SPA; CORS configured by the backend.
- TypeScript client generation uses the committed OpenAPI contract; the current
  handwritten client mirrors that surface.
- `Katalog.AppHost` wires the SPA with `AddViteApp("web", "../web")`, references
  the API, and waits for it before starting the web resource.
