---
type: "Reference"
title: "Katalog - Operations"
description: "How to run the Katalog stack locally: secrets, stable ports, local dependencies, database reset, Spotify rate limits and quota choices, and health/retry behavior."
tags: [operations, aspire, spotify, postgres, secrets, rate-limits, health-checks]
openwiki_generated: true
sources:
  - id: openwiki-source-164e2da859b5277df81c7d94
    resource: repo://.github/workflows/ci.yml
  - id: openwiki-source-58ae3d06ebcb0a21c5b26e22
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/ReleasePoller.cs
  - id: openwiki-source-0e9cacfbaf4c025a152cc799
    resource: repo://apps/katalog-api/src/Katalog.Api/Features/Releases/Polling/ReleasesPollingService.cs
  - id: openwiki-source-bf4ce253af065f2bf88b51c6
    resource: repo://apps/katalog-api/src/Katalog.Api/Infrastructure/Spotify/SpotifyApiClient.cs
  - id: openwiki-source-174bb83681f9e1ec5d5575a6
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/DatabaseMigrationRunner.cs
  - id: openwiki-source-189a20d60246dfdfb96a4668
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/PollingOptions.cs
  - id: openwiki-source-608cc5c60ce2f34e950e04df
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/SpotifySetup.cs
  - id: openwiki-source-3027c8f1fc8c1063abb87bad
    resource: repo://apps/shared/Katalog.ServiceDefaults/KatalogServiceDefaultsExtensions.cs
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
  - id: openwiki-source-02df65b0d854782ae484d583
    resource: repo://Katalog.AppHost/Properties/launchSettings.json
  - id: openwiki-source-4fd82268b1f7ce8f04d0e00c
    resource: repo://Katalog.AppHost/Resources/Api/KatalogApi.cs
  - id: openwiki-source-8959a04ec0f63cd2b5aa0073
    resource: repo://Katalog.AppHost/Resources/Infrastructure/PostgresResourceBuilderExtensions.cs
generated: { by: "openwiki/0.6.0", at: "2026-09-29T18:32:14.763Z" }
verified:
  - by: openwiki/0.6.0
    at: 2026-09-29T18:32:14.763Z
---

# Katalog - Operations

## Current deployment state

Nothing is deployed yet. The repository has:

- a frontend (`web/`) with CI through `.github/workflows/web.yml`;
- the checked-in .NET API, Aspire AppHost, PostgreSQL resource, and committed
  OpenAPI contract - all exercised by `.github/workflows/ci.yml` (build + test,
  plus a contract-drift gate that regenerates the OpenAPI documents and fails on
  any diff under `contracts/`);
- a no-mistakes gate (`git push no-mistakes <branch>`);
- no production deployment yet.

## Secrets

Spotify client credentials are stored as **dotnet user-secrets** on the API
project (keys `Spotify:ClientId` and `Spotify:ClientSecret`), never committed:

```sh
dotnet user-secrets set "Spotify:ClientId" "..." --project apps/katalog-api/src/Katalog.Api
dotnet user-secrets set "Spotify:ClientSecret" "..." --project apps/katalog-api/src/Katalog.Api
```

`ClientId`/`ClientSecret` must never appear in code, appsettings, logs, status
lines, or PR descriptions. `SpotifyOptions` validates them at startup (skipped
in OpenAPI-generation mode), so a missing credential fails the host early.

## Stable ports

Every `aspire run` comes up on the same URLs; all three port owners are
committed to the repo:

| Resource | URL | Pinned in |
|---|---|---|
| Aspire dashboard / AppHost | `https://localhost:15000` | `Katalog.AppHost/Properties/launchSettings.json` `applicationUrl` |
| API | `http://localhost:5192` | `WithHttpEndpoint(port: 5192)` in `Katalog.AppHost/Resources/Api/KatalogApi.cs` |
| Web frontend | `http://localhost:5173` | `WithHttpEndpoint(port: 5173)` in `Katalog.AppHost/Program.cs` |

The launch profile is the mechanism the Aspire CLI 13.5.x uses to pin the
dashboard port - without the committed `launchSettings.json` the CLI assigns a
random port per run. The web port must match Vite's own default so
`vite --port 5173` serves the frontend where the docs say it lives, and the
fixed API port is what the dev-proxy fallback in `web/vite.config.ts`
(`http://localhost:5192`) targets when Aspire has not injected `API_HTTP`.

## Local dependencies

- .NET SDK 10 (`global.json` pinned), Aspire CLI 13.5.4
  (`dotnet tool install -g Aspire.Cli --version 13.5.4`), Docker (for Aspire containers),
  Node.js + npm (frontend).
- Backend dev flow: `aspire run` from the solution root starts Postgres, the API,
  and the frontend resource; the Aspire dashboard shows traces/logs. The API runs
  in the **Development** environment under the host (`Katalog.AppHost` forces
  `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT` on the api resource), which is
  what loads the Spotify user-secrets and enables startup migrations - without
  it the API would start in Production, skip both, and fail options validation.
- The `catalog` Postgres resource exposes a **Reset Database** dashboard action
  (command name `reset-db`, enabled while the resource is healthy) that drops and
  recreates the database via an admin connection to the `postgres` maintenance
  database (`DROP DATABASE ... WITH (FORCE)` then plain `CREATE`), with a
  confirmation prompt; also callable from the CLI with
  `aspire resource catalog reset-db`. All data is lost on reset.

## Environment variables

| Variable | Used by | Meaning |
|---|---|---|
| `API_HTTP` / `API_HTTPS` | `web/vite.config.ts` | Aspire-injected API endpoint for the dev proxy (fallback `http://localhost:5192`) |
| `Spotify:ClientId` / `Spotify:ClientSecret` | backend (user-secrets) | Spotify app credentials (client credentials flow) |
| `Spotify:Market` | backend | ISO 3166-1 market for catalog calls (default `SE`) |
| `Polling:Interval` | backend | Release-poll rhythm; must be 6-24 h (default 12 h), validated at startup |

## Performance and rate limits

The backend poller must respect Spotify's rolling 30-second rate limits and
honor `Retry-After` on 429s (client-credentials token, batch requests, low
poll frequency). Dev-mode quota is small - do not burn it on user-token flows
(MVP has none) or unbounded polling.

Quota shape of release discovery: each poll pass runs a **paginated label
search** (`v1/search?q=label:"<name>"&type=album`, limit clamped to Spotify's
max 10 per request, following `next` URLs) per followed label, and then -
because Spotify's label filter matches fuzzily - issues **one
`GET /albums/{id}` per search candidate** to verify the album's real label on
the full album object. That exact verification is the captain's explicit quota
choice: correctness of label attribution over request count. Keep this in mind
before lowering the poll interval or following high-volume labels; the 6-24 h
rhythm (`PollingOptions.Validate`) is what keeps the per-candidate GETs inside
the dev-mode quota.

The HTTP pipeline is sized for this: a custom Polly pipeline
(`TotalTimeout 300 s → Retry (3 attempts, exponential + jitter, honors
Retry-After) → CircuitBreaker (30 s) → AttemptTimeout 15 s`) replaces the
standard resilience handler, whose 30 s total timeout would guillotine long
Spotify `Retry-After` waits.

## Health

The backend exposes `/health` and `/alive`; `/alive` reflects only the
`live`-tagged self check and is mapped in every environment because the AppHost
health check uses it. Aspire wires the API resource with `WaitFor(catalogDb)`
plus `WithHttpHealthCheck("/alive")`, so the API does not report "Running"
before Postgres is up and startup migrations have run.

The startup migration runner runs in Development and retries transient database
failures with bounded backoff (up to 5 attempts, 1-4 s delays, cancellation
never retried), so `Failed executing DbCommand ... SELECT migration_id ...`
lines right after Postgres reports healthy are retry noise, not a crash - the
first connections pass through the Aspire DCP endpoint proxy, which can briefly
reject connections right after the container reports healthy. Persistent
failures still surface with the full exception once attempts are exhausted.
The same binary also supports `--migrate` and `--rollback <MigrationName>` as
standalone CLI operations.
