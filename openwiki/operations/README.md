# Katalog - Operations

## Current deployment state

Nothing is deployed yet. The repository has:

- a frontend (`web/`) with CI through `.github/workflows/web.yml`;
- a no-mistakes gate (`git push no-mistakes <branch>`);
- no backend on `main` yet (in progress on a feature branch).

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
  (`dotnet tool install -g Aspire.Cli`), Docker (for Aspire containers),
  Node.js + npm (frontend).
- Backend dev flow (once merged): `aspire run` from the solution root starts
  Postgres + the API + the frontend resource; the Aspire dashboard shows
  traces/logs.

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

Planned backend: `/health` + `/alive` endpoints with health checks wired
through Aspire `WaitFor` so the API starts only after Postgres is ready.