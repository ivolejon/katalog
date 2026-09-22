# Project agent memory

This file is the project's committed home for project-intrinsic agent knowledge: build, test, release, architecture, and sharp-edge notes that should travel with the code.

- Add durable project-specific notes here as they are discovered through real work.

## Frontend (web/)

- Vue 3 + TypeScript + Vite SPA built with shadcn-vue preset **a1AhVxI** (reka-ui, Hugeicons, Figtree, neutral, Tailwind 4). Setup, dev proxy and commands: `web/README.md`.
- API client is handwritten until the backend commits `contracts/katalog-api/openapi.json`; then run `npm run generate:client` in `web/` (@hey-api/openapi-ts). Endpoint map + switch procedure: `web/src/api/README.md`.
- shadcn-vue emits `@hugeicons/vue` icon imports that the package does not export; bridge lives in `src/lib/icons.ts` (`web/README.md` "UI kit notes"). Extend it when re-adding components.
- AppHost integration details: `web/README.md` "AppHost integration".

## Backend (owned by the backend worker)

- Layout and conventions follow the arch report `data/katalog-arch-ref-q1/report.md` (single API project, contracts/ committed OpenAPI, no user auth in MVP).

## Katalog (label-följning via Spotify)

- **Designkälla:** `data/katalog-research-q1/report.md` (research) och `data/katalog-arch-ref-q1/report.md` (arkitekturgranskning) ligger i firstmates datakatalog; README.md sammanfattar besluten.
- Frontend `web/` orkestreras av `Katalog.AppHost` via `AddViteApp`; frontend-specifik setup
  och konventioner finns i `web/README.md`.
- **Bygge kräver Aspire CLI** (annars ASPIRE009): `dotnet tool install -g Aspire.Cli --version 13.5.4`. Dev-run: `aspire run` (startar Postgres-container + API via Katalog.AppHost).
- **OpenAPI-kontrakt:** `contracts/katalog-api/openapi.json` genereras vid varje `dotnet build` (Directory.Build.props-styrt, av i CI) och commit:as; grinden är contract-drift i `.github/workflows/ci.yml`. Alla endpoints har `operationId`.
- **OpenAPI-genereringsläget** (`GetDocument.Insider`) får aldrig kräva levande Postgres/Spotify: `appsettings.OpenApiGeneration.json` har placeholder-förbindelser och setups guardas med `OpenApiDocumentGeneration.IsActive` (inkl. ValidateOnStart-options och hosted services).
- **Spotify-credentials:** aldrig i appsettings; user secrets under `Spotify:ClientId`/`Spotify:ClientSecret` (UserSecretsId finns i Katalog.Api.csproj). Options valideras vid start, skippas i OpenAPI-läget.
- **Spotify Web API docs:** https://developer.spotify.com/documentation/web-api (referens + koncept) och den auktoritativa OpenAPI-specen https://developer.spotify.com/reference/web-api/open-api-schema.yaml (primär källa för endpoint-fakta - verifiera alltid limit- och parametervärden där, inte i gamla guides). Aktuella tak (verifierat 2026-09-22): Spotify search och get-an-artists-albums har API-default 5 och max 10; högre limit ger HTTP 400 "Invalid limit". Katalogs sökstandard är 10, och appen skickar aldrig limit > 10 på search/albums; get-followed/recently-played ligger kvar på max 50.
- **Databas:** EF Core 10 + Npgsql, `ConnectionStrings:catalog`. Schema-migrering vid start i dev; `--migrate`/`--rollback` i samma binär. Konventioner (se Infrastructure/Configurations): inga JSON-kolumner, junction-tabeller, enums som int med gaps, `Guid.CreateVersion7()`/`uuidv7()`.
- **Tester:** `dotnet test Katalog.slnx` - unit + integration (Testcontainers.PostgreSql postgres:18.3 + WireMock) + AppHost-smoke. Kräver Docker. Integration testas mot WireMock, aldrig riktig Spotify.

## Maintaining this file

Keep this file for knowledge useful to almost every future agent session in this project.
Do not repeat what the codebase already shows; point to the authoritative file or command instead.
Prefer rewriting or pruning existing entries over appending new ones.
When updating this file, preserve this bar for all agents and keep entries concise.
