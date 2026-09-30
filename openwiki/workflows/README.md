---
type: "Reference"
title: "Katalog - Workflows"
description: "Developer and delivery workflows for Katalog: the .NET and npm command sets, the Aspire run flow, the CI jobs (build/test, web test, contract drift), the no-mistakes gate and its commands, and OpenWiki maintenance."
tags: [workflows, ci, aspire, npm, dotnet, no-mistakes, openapi, openwiki]
openwiki_generated: true
sources:
  - id: openwiki-source-164e2da859b5277df81c7d94
    resource: repo://.github/workflows/ci.yml
  - id: openwiki-source-2654958c56ab19e290fb19a3
    resource: repo://.github/workflows/web.yml
  - id: openwiki-source-acaa950932ec8af34914b8e0
    resource: repo://.no-mistakes.yaml
  - id: openwiki-source-8037e2358a2c4f9b2c722a11
    resource: repo://AGENTS.md
  - id: openwiki-source-55efad680bc588e00d3fa736
    resource: repo://apps/katalog-api/src/Katalog.Api/Infrastructure/OpenApiDocumentGeneration.cs
  - id: openwiki-source-ed67428df068a79226230f71
    resource: repo://apps/katalog-api/src/Katalog.Api/Katalog.Api.csproj
  - id: openwiki-source-40176359058e84debec9e8ac
    resource: repo://apps/katalog-api/src/Katalog.Api/Program.cs
  - id: openwiki-source-174bb83681f9e1ec5d5575a6
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/DatabaseMigrationRunner.cs
  - id: openwiki-source-85aa94c9c1be905c180a3801
    resource: repo://apps/katalog-api/src/Katalog.Api/Setup/OptionsSetup.cs
  - id: openwiki-source-d7d53fc1c8568e36a3849e3c
    resource: repo://aspire.config.json
  - id: openwiki-source-1601dc4304e3854313f15d32
    resource: repo://Directory.Build.props
  - id: openwiki-source-3fdadfb804ef0399cbe9e4c6
    resource: repo://dotnet-tools.json
  - id: openwiki-source-1d696820276aca292ffb0f17
    resource: repo://Katalog.AppHost/Katalog.AppHost.csproj
  - id: openwiki-source-8ce73889fe4fb27ed1786287
    resource: repo://Katalog.AppHost/Program.cs
  - id: openwiki-source-4fd82268b1f7ce8f04d0e00c
    resource: repo://Katalog.AppHost/Resources/Api/KatalogApi.cs
  - id: openwiki-source-8959a04ec0f63cd2b5aa0073
    resource: repo://Katalog.AppHost/Resources/Infrastructure/PostgresResourceBuilderExtensions.cs
  - id: openwiki-source-23775c3de52f3ab95a13cb8b
    resource: repo://README.md
  - id: openwiki-source-14e56945b7c632a3b335dcec
    resource: repo://web/package.json
  - id: openwiki-source-52cc4c3bc39b1652ba919753
    resource: repo://web/README.md
  - id: openwiki-source-ca248e99bf5d44b0aa64b70c
    resource: repo://web/src/api/README.md
  - id: openwiki-source-d9b28f691af698304a6f2f32
    resource: repo://web/vite.config.ts
generated: { by: "openwiki/0.6.0", at: "2026-09-30T03:07:03.981Z" }
verified:
  - by: openwiki/0.6.0
    at: 2026-09-30T03:07:03.981Z
---

# Katalog - Workflows

The command and check reference for the repository: what you run locally, what
GitHub runs, and what the no-mistakes gate requires before a change reaches
`main`. For *what each command verifies*, see
[Testing](../testing/README.md); for ports, secrets, health endpoints, and
migration behaviour, see [Operations](../operations/README.md); for how the SPA,
API, and database fit together, see
[Architecture](../architecture/README.md).

## Prerequisites

- **.NET SDK 10.0.100** (`rollForward: latestFeature`, `global.json`) plus
  Docker - `dotnet test` needs Docker for Testcontainers and the AppHost smoke
  run.
- **Aspire CLI 13.5.4** on `PATH`:
  `dotnet tool install -g Aspire.Cli --version 13.5.4`. The AppHost project uses
  `Aspire.AppHost.Sdk/13.5.4`, so the build fails with `ASPIRE009` without the
  CLI; CI installs the same version in both .NET jobs.
- **Node.js + npm** for the frontend.
- The local **Spotify credentials** (user secrets) - see
  [Operations](../operations/README.md#spotify-credentials).

## Backend commands

Run from the repository root; the solution is `Katalog.slnx`.

```sh
dotnet restore Katalog.slnx
dotnet build Katalog.slnx          # also regenerates contracts/katalog-api/openapi.json
dotnet test Katalog.slnx           # unit + integration (Testcontainers) + AppHost smoke
aspire run                         # PostgreSQL + API + web
```

`dotnet build` regenerates the committed OpenAPI contract as a side effect
during local builds (see [The contract and contract drift](#the-contract-and-contract-drift)).
`aspire run` is driven by `aspire.config.json`, which points the CLI at
`Katalog.AppHost/Katalog.AppHost.csproj`; the stack it starts is described in
[Operations](../operations/README.md).

### Migrations

Migrations run automatically at API startup outside Staging/Production. For a
deploy, a manual apply, or a rollback, use the local `dotnet-ef` tool
(`dotnet tool restore`, pinned 10.0.12 in `dotnet-tools.json`) or the
`--migrate` / `--rollback` CLI mode built into the API binary:

```sh
dotnet tool restore
dotnet tool run dotnet-ef --project apps/katalog-api/src/Katalog.Api migrations list
dotnet apps/katalog-api/src/Katalog.Api/bin/Debug/net10.0/Katalog.Api.dll --migrate
dotnet apps/katalog-api/src/Katalog.Api/bin/Debug/net10.0/Katalog.Api.dll --rollback 0
```

`--migrate` / `--rollback` short-circuit `Program.cs` into
`DatabaseMigrationRunner.CreateMigrationBuilder(...).RunAsync(...)` before any
web host is built, and return a process exit code (0 success, 1 failure).

## Frontend commands

Run from `web/`. The SPA calls relative `/api/*` paths, so it needs a backend
(either under `aspire run` or separately) to be useful.

```sh
npm install
npm run dev              # vite, http://localhost:5173
npm run typecheck        # vue-tsc -b --noEmit
npm test                 # vitest run (frontend unit tests)
npm run build            # vue-tsc -b && vite build
npm run preview          # serve the production build
npm run generate:client  # @hey-api/openapi-ts -> src/api/generated
```

The dev server proxies `/api` to the backend: `API_HTTP`/`API_HTTPS` when Aspire
runs the app as a resource, otherwise the `http://localhost:5192` fallback
(`web/vite.config.ts`).

`generate:client` reads the committed contract
(`../contracts/katalog-api/openapi.json`) and writes `@hey-api/openapi-ts`
output to `src/api/generated/`. The client actually shipped in `src/api/` is
still handwritten; the switch procedure and the per-endpoint map live in
`web/src/api/README.md`.

## The Aspire run flow

`aspire run` builds one `DistributedApplication` in `Katalog.AppHost/Program.cs`:

1. `postgres` server with a `catalog` database carrying the `reset-db`
   dashboard action (`WithResetCommand`).
2. `api` - the `Katalog.Api` project, forced to the Development environment,
   with `WaitFor(catalogDb)` and an HTTP health check on `/alive`, on fixed host
   port 5192.
3. `web` - `AddViteApp("web", "../web", runScriptName: "dev")`, which runs
   `npm run dev` in `web/`, receives the API endpoint as `API_HTTP`
   (`WithReference(api)` + `WithEnvironment`), and only starts after the API is
   ready (`WaitFor(api)`), on fixed host port 5173.

Because `web` is a real Aspire resource, `aspire run` also drives the frontend
end-to-end; running `npm run dev` by hand is the standalone alternative. Port
ownership and reset/migration details are in
[Operations](../operations/README.md).

## The contract and contract drift

`contracts/katalog-api/openapi.json` is the coupling point between the API and
the frontend client, and it is **regenerated at build time**:

- `Directory.Build.props` sets `OpenApiGenerateDocuments=true` by default and
  turns it off when `CI=true` or the environment is Staging/Production, so
  routine local builds refresh the file but CI builds do not.
- `Katalog.Api.csproj` points `OpenApiDocumentsDirectory` at `contracts/katalog-api`
  and generates an OpenAPI 3.1 document named `openapi`.
- The generator host (`GetDocument.Insider`) must never need a live
  Postgres/Spotify: `OpenApiDocumentGeneration.IsActive` is checked in setup so
  that placeholder settings from `appsettings.OpenApiGeneration.json` are used
  and options validation, hosted services, and startup migrations are skipped.

So after changing an endpoint, `dotnet build Katalog.slnx` locally and commit
the regenerated contract; the drift check in CI is what enforces it.

## CI

Two workflows, both read-only (`permissions: contents: read`).

`.github/workflows/ci.yml` - runs on pull requests and on pushes to `main`,
with `concurrency: ci-${{ github.ref }}` and `cancel-in-progress: true`:

| Job | What it does |
|---|---|
| `build-test` | checkout, `setup-dotnet` (from `global.json`), install Aspire CLI 13.5.4, `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release` over the whole solution (unit + Testcontainers/WireMock integration + AppHost smoke). |
| `web-test` | Node 24 + `npm ci` in `web/`, then `npm test` (vitest). |
| `contract-drift` | restore, then `dotnet build -c Release /p:OpenApiGenerateDocuments=true` to force regeneration, then `git diff --exit-code -- contracts/` - any change to the committed contract fails the job. |

`.github/workflows/web.yml` - runs on pull requests targeting `main` only: Node
22, `npm ci`, `npm run typecheck`, `npm run build` in `web/`.

Two consequences worth knowing before changing CI:

- The frontend is verified under **two different Node majors** (`24` in
  `ci.yml`, `22` in `web.yml`), and the repository pins no Node version for
  `web/` (no `.nvmrc`/engines field), so the local Node version is whatever the
  developer has.
- `ci.yml` is also the workflow the no-mistakes gate's CI step monitors - its
  `build-test` job (extended with the .NET jobs per the arch report) is what
  registers the GitHub checks the gate waits for, and `.no-mistakes.yaml`
  states that its `commands` mirror these steps as a requirement of
  `no-mistakes ci-workflow`. Adding, renaming, or removing a job changes what
  the gate sees.

## The no-mistakes gate

Change requests are pushed through the gate rather than straight to `origin`:

```sh
git push no-mistakes <branch>
```

`.no-mistakes.yaml` is the gate configuration, and it is read **only from the
trusted default branch** (`main`). Changes to the control fields - the agent, the
OCR settings, and the `commands` - therefore only take effect once they land on
`main`; a pushed branch cannot weaken the gate.

What the file currently configures:

- **`agent: pi`** - the pipeline agent is `pi`, the logged-in LLM provider for
  this harness on this machine. (It is also the LLM used for the OCR gate in
  delegation mode.)
- **`ocr.enabled: true`, `ocr.delegate: true`** - the OpenCodeReview gate runs
  directly after the review, and requires the external `ocr` CLI on the daemon
  host (`npm install -g @alibaba-group/open-code-review`). With
  `delegate: true`, `ocr` does the deterministic work (file selection, rule
  groups) while the LLM review itself is done by the pipeline agent, so no
  separate OCR LLM configuration is needed.

| Stage | Command | Mirrors |
|---|---|---|
| `prepare` | `dotnet restore Katalog.slnx` | CI restore |
| `lint` | `dotnet build Katalog.slnx --no-restore -c Release` | the CI build - the build *is* the lint step |
| `test` | `dotnet test Katalog.slnx --no-restore -c Release` | the CI test step |
| `format` | `dotnet format Katalog.slnx --no-restore --verify-no-changes` | no CI equivalent; formatting drift fails the gate |

`lint` is just the build because `TreatWarningsAsErrors` is set globally in
`Directory.Build.props` (with an explicit `WarningsNotAsErrors` list for package
advisories `NU1901`-`NU1904`, the known `NU1608` satellite mismatch, and
deprecated-Spotify `CS0612`/`CS0618` usage) - a warning outside that list fails
the build and therefore fails the gate.

## OpenWiki maintenance

The generated documentation lives in `openwiki/`. After meaningful repository
changes, run an update so only affected pages are regenerated:

```sh
/openwiki:update   # pi session; updates only affected docs
```

`openwiki/.last-update.json` records the last generation (command, model, git
head, status) that `update` uses to detect what changed. Generated pages should
not be hand-edited; change the code or the source docs and let the run
regenerate. The generated pages themselves instruct agents to use just-in-time
retrieval rather than preloading the wiki (see the OpenWiki block in
`AGENTS.md`).

## Where to go next

- [Quickstart](../quickstart.md) - what Katalog is and the shortest run path.
- [Architecture](../architecture/README.md) - SPA, API, database, AppHost, and
  the committed contract.
- [Operations](../operations/README.md) - prerequisites, fixed ports, Spotify
  credentials, health endpoints, reset/migration behaviour.
- [Testing](../testing/README.md) - what the unit, integration, AppHost smoke,
  and vitest suites cover and the rules for extending them.
