---
type: "Reference"
title: "Katalog - Operations"
openwiki_generated: true
verified:
  - by: openwiki/0.5.2
    at: 2026-09-22T17:28:24.726Z
sources:
  - id: openwiki-source-164e2da859b5277df81c7d94
    resource: repo://.github/workflows/ci.yml
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
  - id: openwiki-source-4fd82268b1f7ce8f04d0e00c
    resource: repo://Katalog.AppHost/Resources/Api/KatalogApi.cs
generated: { by: "opencode", at: "2026-09-22T17:28:24.726Z" }
---

# Katalog - Operations

## Current deployment state

Nothing is deployed yet. The repository has:

- a frontend (`web/`) with CI through `.github/workflows/web.yml`;
- the checked-in .NET API, Aspire AppHost, PostgreSQL resource, and committed
  OpenAPI contract;
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
lines, or PR descriptions.

## Local dependencies

- .NET SDK 10 (`global.json` pinned), Aspire CLI 13.5.4
  (`dotnet tool install -g Aspire.Cli --version 13.5.4`), Docker (for Aspire containers),
  Node.js + npm (frontend).
- Backend dev flow: `aspire run` from the solution root starts Postgres, the API,
  and the frontend resource; the Aspire dashboard shows traces/logs.

## Environment variables

| Variable | Used by | Meaning |
|---|---|---|
| `API_HTTP` / `API_HTTPS` | `web/vite.config.ts` | Aspire-injected API endpoint for the dev proxy |
| `Spotify:ClientId` / `Spotify:ClientSecret` | backend (user-secrets) | Spotify app credentials (client credentials flow) |
| `Spotify:Market` | backend | ISO 3166-1 market for catalog calls (default `SE`) |

## Performance and rate limits

The backend poller must respect Spotify's rolling 30-second rate limits and
honor `Retry-After` on 429s (client-credentials token, batch requests, low
poll frequency). Dev-mode quota is small - do not burn it on user-token flows
(MVP has none) or unbounded polling.

## Health

The backend exposes `/health` and `/alive`; Aspire wires the API resource through
`WaitFor` so it starts only after Postgres is ready.
