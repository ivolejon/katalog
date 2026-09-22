# Katalog - Quickstart

Katalog is an app for following record labels ("labels") through the Spotify
catalog. A single user tracks a set of labels, each with linked artists, and
sees their new releases in one place.

- **Product intent:** follow labels via Spotify; the user adds the labels to
  track (manual, via Spotify artist search). Single user, no login in the MVP.
- **Stack:** Vue 3 + TypeScript SPA (`web/`, shadcn-vue "reka-maia" a1AhVxI
  preset, Tailwind CSS 4), backend .NET 10 + Aspire + PostgreSQL (in progress
  on a feature branch - not yet merged).

## Current state (lived-in reality)

The repository currently contains the **frontend only**:

- `web/` - the complete Vue SPA (landing, labels overview, label detail with
  artist/release tabs, Spotify-linked album cards).
- `.github/workflows/web.yml` - CI for the web app (build + typecheck).
- `.no-mistakes.yaml` - the no-mistakes gate configuration (opencode agent,
  OCR delegation).
- `AGENTS.md` / `CLAUDE.md` - project agent memory.

The backend (.NET 10 + Aspire + Postgres API, OpenAPI contract) is being built
in parallel on a branch and will land as the backend PR. Until then the
frontend talks to the backend through a **handwritten typed API client**
(`web/src/api/`) matching the agreed endpoint surface.

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

- [Architecture](architecture/README.md) - how the SPA is put together and how
  it will connect to the backend.
- [Domain](domain/README.md) - the label-following business model and the
  Spotify API constraints behind it.
- [Workflows](workflows/README.md) - development commands, CI, the no-mistakes
  gate, and the OpenWiki update flow.
- [Operations](operations/README.md) - secrets, environments, and deployment
  state.
- [Testing](testing/README.md) - what is verified today and the planned
  integration/e2e strategy.