---
type: "Reference"
title: "Katalog - Quickstart"
openwiki_generated: true
verified:
  - by: openwiki/0.5.2
    at: 2026-09-22T17:28:24.726Z
sources:
  - id: openwiki-source-40176359058e84debec9e8ac
    resource: repo://apps/katalog-api/src/Katalog.Api/Program.cs
  - id: openwiki-source-c4e6278c448892ab6ee9060c
    resource: repo://contracts/katalog-api/openapi.json
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
  - id: openwiki-source-23775c3de52f3ab95a13cb8b
    resource: repo://README.md
generated: { by: "opencode", at: "2026-09-22T17:28:24.726Z" }
---

# Katalog - Quickstart

Katalog is an app for following record labels ("labels") through the Spotify
catalog. A single user tracks a set of labels, each with linked artists, and
sees their new releases in one place.

- **Product intent:** follow labels via Spotify; the user adds the labels to
  track (manual, via Spotify artist search). Single user, no login in the MVP.
- **Stack:** Vue 3 + TypeScript SPA (`web/`, shadcn-vue "reka-maia" a1AhVxI
  preset, Tailwind CSS 4), backend .NET 10 + Aspire + PostgreSQL.

## Current state (lived-in reality)

The repository contains the complete local application stack:

- `web/` - the complete Vue SPA (landing, labels overview, label detail with
  artist/release tabs, Spotify-linked album cards).
- `apps/katalog-api/` - the .NET API, EF Core persistence, and release poller.
- `Katalog.AppHost/` - Aspire orchestration for PostgreSQL, the API, and web.
- `contracts/katalog-api/openapi.json` - the committed OpenAPI contract.
- `.github/workflows/ci.yml` and `.github/workflows/web.yml` - backend and
  frontend CI.
- `.no-mistakes.yaml` - the no-mistakes gate configuration (opencode agent,
  OCR delegation).
- `AGENTS.md` / `CLAUDE.md` - project agent memory.

The frontend talks to the backend through a **handwritten typed API client**
(`web/src/api/`) while the committed contract is available for generated-client
adoption.

## Run the full stack

```sh
dotnet restore Katalog.slnx
dotnet build Katalog.slnx
dotnet test Katalog.slnx
aspire run
```

## Run the frontend

```sh
cd web
npm install
npm run dev          # http://localhost:5173
npm run typecheck    # vue-tsc, no emit
npm run build        # typecheck + production build
```

In dev, `vite.config.ts` proxies `/api` to the backend: `API_HTTP`/`API_HTTPS`
(Aspire-injected when the app runs as an `AddViteApp` resource) with a
`http://localhost:5192` fallback.

## Where to go next

- [Architecture](architecture/README.md) - how the SPA, API, database, and
  Aspire host fit together.
- [Domain](domain/README.md) - the label-following business model and the
  Spotify API constraints behind it.
- [Workflows](workflows/README.md) - development commands, CI, the no-mistakes
  gate, and the OpenWiki update flow.
- [Operations](operations/README.md) - secrets, environments, and deployment
  state.
- [Testing](testing/README.md) - frontend and backend verification layers.
