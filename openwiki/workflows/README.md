---
type: "Reference"
title: "Katalog - Workflows"
openwiki_generated: true
verified:
  - by: openwiki/0.5.2
    at: 2026-09-22T17:28:24.726Z
sources:
  - id: openwiki-source-164e2da859b5277df81c7d94
    resource: repo://.github/workflows/ci.yml
  - id: openwiki-source-2654958c56ab19e290fb19a3
    resource: repo://.github/workflows/web.yml
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
  - id: openwiki-source-23775c3de52f3ab95a13cb8b
    resource: repo://README.md
  - id: openwiki-source-14e56945b7c632a3b335dcec
    resource: repo://web/package.json
generated: { by: "opencode", at: "2026-09-22T17:28:24.726Z" }
---

# Katalog - Workflows

## Frontend development

```sh
dotnet restore Katalog.slnx
dotnet build Katalog.slnx       # regenerates contracts/katalog-api/openapi.json
dotnet test Katalog.slnx        # unit, integration, and AppHost smoke tests
aspire run                      # PostgreSQL + API + web

cd web
npm install
npm run dev              # Vite dev server, http://localhost:5173
npm run typecheck        # vue-tsc -b --noEmit
npm run build            # typecheck + production build
npm run generate:client  # @hey-api/openapi-ts -> src/api/generated
```

The dev server proxies `/api` to the backend on `API_HTTP`/`API_HTTPS`
(Aspire-injected) or falls back to `http://localhost:5192`.

## CI

`.github/workflows/ci.yml` runs solution build/tests and checks OpenAPI contract
drift. `.github/workflows/web.yml` separately builds and typechecks the web app.

## The no-mistakes gate

`.no-mistakes.yaml` configures the no-mistakes validation gate (agent:
`opencode`, OCR review delegation enabled). Change requests push through the
gate rather than straight to `origin`:

```sh
git push no-mistakes <branch>
```

Gate settings are read only from the trusted default branch; feature branches
cannot weaken them.

## OpenWiki maintenance

OpenWiki documentation lives in `openwiki/`. After meaningful repo changes:

```sh
/openwiki:update   # pi session; updates only affected docs
```

`openwiki/.last-update.json` records the last generation (command, model,
git head, snapshot hash) that `update` uses to detect changes.

## Branch and delivery conventions

- Feature branches from `main`, semantic commit messages
  (e.g. `feat(web): ...`).
- The committed contract (`contracts/katalog-api/openapi.json`) is the coupling
  point between the frontend client and backend.
- Merge authority: the captain approves every merge (no autonomous merge).
