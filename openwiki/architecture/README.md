---
type: "Reference"
title: "Katalog - Architecture"
openwiki_generated: true
verified:
  - by: openwiki/0.5.2
    at: 2026-09-24T00:35:33.000Z
sources:
  - id: openwiki-source-40176359058e84debec9e8ac
    resource: repo://apps/katalog-api/src/Katalog.Api/Program.cs
  - id: openwiki-source-c4e6278c448892ab6ee9060c
    resource: repo://contracts/katalog-api/openapi.json
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
generated: { by: "pi", at: "2026-09-24T00:35:33.000Z" }
---

# Katalog - Architecture

## System overview

```
┌──────────────────────┐   relative /api/*   ┌──────────────────────┐
│ web/ (Vue SPA)       │ ──────────────────> │ Katalog.Api            │
│ shadcn-vue reka-maia │   dev: Vite proxy   │ .NET 10 minimal API   │
└──────────────────────┘                     └──────────┬───────────┘
                                                        │ EF Core 10 + Npgsql
                                                 ┌──────▼──────┐
                                                 │ PostgreSQL  │
                                                 │ ("catalog") │
                                                 └─────────────┘
```

The checked-in backend is a .NET 10 minimal API hosted by `Katalog.Api`.
`Katalog.AppHost` starts it with PostgreSQL and the Vue app through Aspire;
`contracts/katalog-api/openapi.json` is the committed build-generated contract.
`Katalog.AppHost` forces the api resource to the **Development** environment
(`ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`) so Spotify user-secrets load and
the startup migration runner runs, and the catalog database resource exposes a
"Reset Database" dashboard action (`reset-db`) that drops and recreates the
database for a fresh start.

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
- `src/composables/useArtistSearch.ts` - debounced Spotify artist search for
  the add-label combobox (empty queries reset without hitting the API).
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
Aspire orchestration is provided by `Katalog.AppHost`. No user OAuth and no app
auth are used in the MVP - all Spotify data comes from a client-credentials app
token.
Source of truth: `data/katalog-arch-ref-q1/report.md` (external to this repo).

## Frontend/backend coupling decisions

- Relative `/api` calls from the SPA; CORS configured by the backend.
- TypeScript client generation uses the committed OpenAPI contract; the current
  handwritten client mirrors that surface.
- `Katalog.AppHost` wires the SPA with `AddViteApp("web", "../web")`, references
  the API, and waits for it before starting the web resource.
