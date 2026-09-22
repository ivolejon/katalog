# Katalog - Architecture

## System overview

```
┌──────────────────────┐   relative /api/*   ┌──────────────────────┐
│ web/ (Vue SPA)       │ ──────────────────> │ Katalog.Api (planned) │
│ shadcn-vue reka-maia │   dev: Vite proxy   │ .NET 10 minimal API   │
└──────────────────────┘                     └──────────┬───────────┘
                                                        │ EF Core 10 + Npgsql
                                                 ┌──────▼──────┐
                                                 │ PostgreSQL  │
                                                 │ ("catalog") │
                                                 └─────────────┘
```

The backend does not exist on `main` yet - it is under construction on a
feature branch (see `web/src/api/README.md` for the agreed contract surface).
Final wiring (Aspire `AddViteApp` + contract-generated types) lands after the
backend PR.

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
  `labels.ts`; `API_CLIENT_ORIGIN = 'handwritten'`); switch to the generated
  `@hey-api/openapi-ts` client when `contracts/katalog-api/openapi.json`
  lands (`npm run generate:client`). Endpoint map: see `src/api/README.md`.
- `src/components/` - feature components (`labels/`, `albums/`, `artists/`,
  `common/`, `layout/`) plus generated shadcn-vue primitives in
  `src/components/ui/`.
- `src/lib/icons.ts` - bridge for `@hugeicons/vue` imports shadcn-vue emits
  but the package does not export (extend when re-adding components).

Dev proxy (`vite.config.ts`): SPA always calls relative `/api/*`; Vite proxies
to `API_HTTP`/`API_HTTPS` when injected (Aspire resource) with a
`http://localhost:5192` fallback.

## Planned backend (per architecture report)

Single `Katalog.Api` .NET 10 minimal API with vertical slices, an in-process
release-polling `BackgroundService`, EF Core 10 + Npgsql against one
`catalog` Postgres database, migrations via `--migrate` in the same binary, a
committed OpenAPI contract in `contracts/katalog-api/openapi.json`, and
Aspire orchestration through `Katalog.AppHost`. No user OAuth and no app auth
in the MVP - all Spotify data comes from a client-credentials app token.
Source of truth: `data/katalog-arch-ref-q1/report.md` (external to this repo).

## Frontend/backend coupling decisions

- Relative `/api` calls from the SPA; CORS configured by the backend.
- TypeScript client generated from the committed OpenAPI contract; until the
  contract exists the handwritten client mirrors that surface exactly.
- `Katalog.AppHost` wiring for the SPA (`AddViteApp("web", "../web")`) lands
  once the backend scaffold exists.