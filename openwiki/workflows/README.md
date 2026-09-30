---
type: "Reference"
title: "Katalog - Workflows"
description: "Development commands, CI pipelines, the no-mistakes gate, OpenWiki maintenance, and branch/delivery conventions for the Katalog repository."
tags: [workflows, ci, aspire, no-mistakes, openwiki, openapi, contracts]
openwiki_generated: true
sources:
  - id: openwiki-source-164e2da859b5277df81c7d94
    resource: repo://.github/workflows/ci.yml
  - id: openwiki-source-2654958c56ab19e290fb19a3
    resource: repo://.github/workflows/web.yml
  - id: openwiki-source-acaa950932ec8af34914b8e0
    resource: repo://.no-mistakes.yaml
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
  - id: openwiki-source-02df65b0d854782ae484d583
    resource: repo://Katalog.AppHost/Properties/launchSettings.json
  - id: openwiki-source-23775c3de52f3ab95a13cb8b
    resource: repo://README.md
  - id: openwiki-source-14e56945b7c632a3b335dcec
    resource: repo://web/package.json
  - id: openwiki-source-d9b28f691af698304a6f2f32
    resource: repo://web/vite.config.ts
generated: { by: "openwiki/0.6.0", at: "2026-09-29T18:32:14.763Z" }
verified:
  - by: openwiki/0.6.0
    at: 2026-09-29T18:32:14.763Z
---

# Katalog - Workflows

This page covers the dev/CI/gate mechanics of the repository. The
release-polling and label-verification runtime workflow (Spotify search,
candidate verification, `label_albums` linking) is documented separately in
[Workflows - Release discovery](release-discovery.md).

## Local development

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

The build requires the Aspire CLI on PATH (`dotnet tool install -g Aspire.Cli
--version 13.5.4`); without it the AppHost SDK build fails with ASPIRE009.

`aspire run` orchestrates the whole stack from `Katalog.AppHost/Program.cs`:
`SetupPostgres()` starts a single PostgreSQL server, the `catalog` database is
registered with `WithResetCommand()` (a "Reset Database" dashboard action,
`reset-db`, that drops and recreates the database), `SetupKatalogApi(catalogDb)`
wires the API against that database, and `AddViteApp("web", "../web")` runs the
frontend. All three resources come up on stable ports every run: the dashboard
at `https://localhost:15000` (pinned by the committed
`Katalog.AppHost/Properties/launchSettings.json` `applicationUrl` - the launch
profile is the mechanism the Aspire CLI 13.5.x uses to pin the dashboard port),
the API at `http://localhost:5192` (fixed host port in
`Resources/Api/KatalogApi.cs`), and web at `http://localhost:5173`
(`WithHttpEndpoint(port: 5173)` in `Program.cs`, matching Vite's own default
port).

The Vite dev server proxies `/api` to the backend on `API_HTTP`/`API_HTTPS`
(Aspire-injected when the app runs as an `AddViteApp` resource) or falls back
to `http://localhost:5192`.

## CI

`.github/workflows/ci.yml` (push to `main` and pull requests) has two jobs:

- **Build & test** - restores, builds (`-c Release`, warnings-as-errors via
  `Directory.Build.props`), and runs the full solution test suite (unit,
  Testcontainers integration, AppHost smoke). It installs the Aspire CLI first
  and registers the GitHub checks the no-mistakes gate's CI step monitors.
- **Contract drift** - rebuilds with `/p:OpenApiGenerateDocuments=true` to
  regenerate the OpenAPI documents (placeholder appsettings plus
  `OpenApiDocumentGeneration` guards keep the generator host from touching a
  live Postgres/Spotify) and fails on any `git diff` under `contracts/`.

`.github/workflows/web.yml` (pull requests to `main`) separately installs
Node 22 with `npm ci`, then runs `npm run typecheck` and `npm run build` in
`web/`.

## The no-mistakes gate

`.no-mistakes.yaml` configures the no-mistakes validation gate (agent: `pi`,
OCR review delegation enabled). Change requests push through the gate rather
than straight to `origin`:

```sh
git push no-mistakes <branch>
```

The gate's `commands` (`prepare`/`lint`/`test`/`format`) mirror the CI build
and test steps: `dotnet restore`, `dotnet build` (the lint step - the build
itself is warnings-as-errors), `dotnet test`, and `dotnet format
--verify-no-changes`. Gate settings are read only from the trusted default
branch; feature branches cannot weaken them.

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
  point between the frontend client and backend; it is regenerated on every
  `dotnet build` and enforced by the CI contract-drift job.
- Merge authority: the captain approves every merge (no autonomous merge).
