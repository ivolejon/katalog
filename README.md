# Katalog

Följ skivbolag (labels) via Spotify. Backend i .NET 10 + Aspire, PostgreSQL. Frontend i Vue +
TypeScript med shadcn-vue orkestreras tillsammans med API:t via Aspire.

Arkitekturdesign: `data/katalog-research-q1/report.md` (research) och `data/katalog-arch-ref-q1/
report.md` (arkitekturgranskning) finns i firstmate-datan; de punkterna implementeras här.

## Beslut som styr implementationen

- **En användare (single-user-app)**, ingen användarinloggning i MVP.
- All Spotify-data hämtas med **app-token (client credentials flow)** - ingen Spotify-OAuth.
- Labels och artistkopplingar är **app-egna entiteter** (Spotifys `label`-fält är deprecated).
- **Inga tracks lagras.** MusicBrainz väntar.
- Release-polling per artist: `BackgroundService` + `PeriodicTimer`, 6-24 h rytm,
  idempotent upsert (`ON CONFLICT (spotify_id)`), poll-cursor i DB.

## Struktur

```
Katalog.slnx
global.json / Directory.Build.props / Directory.Packages.props / .editorconfig / aspire.config.json
Katalog.AppHost/            # Aspire-orkestrering: postgres + api (Program.cs + Resources/*)
apps/
  shared/Katalog.ServiceDefaults/   # tunt: OTEL + health + service discovery + resilience
  katalog-api/
    src/Katalog.Api/                # enda API-projekt (minimal API + hosted poller + EF)
    tests/Katalog.Api.Tests/        # xUnit v3: unit + integration (Testcontainers+WireMock) + AppHost-smoke
contracts/katalog-api/openapi.json  # commit:at OpenAPI-kontrakt (build-genererat)
```

## Komma igång

Kräver: .NET SDK 10, Aspire CLI 13.5.4 (`dotnet tool install -g Aspire.Cli`), Docker, Node.

```bash
dotnet restore Katalog.slnx
dotnet build Katalog.slnx            # regenererar contracts/katalog-api/openapi.json vid build
dotnet test                          # unit + integration (Testcontainers) + AppHost-smoke
aspire run                           # hela stacken: Postgres-container + API
```

Spotify-credentials läggs i user secrets (aldrig i kod/appsettings):

```bash
cd apps/katalog-api/src/Katalog.Api
dotnet user-secrets set "Spotify:ClientId" "<client id>"
dotnet user-secrets set "Spotify:ClientSecret" "<client secret>"
```

API:ets health-endpoints: `/alive` (liveness) och `/health`. OpenAPI-dokument live på
`/openapi/v1.json`; kontraktet commitas i `contracts/katalog-api/openapi.json`.

## Migreringar

Migrationer körs automatiskt vid app-start i dev. Manuellt (deploy/återställning):

```bash
dotnet tool restore                 # dotnet-ef 10.0.12 (lokalt verktyg)
dotnet tool run dotnet-ef --project apps/katalog-api/src/Katalog.Api migrations list
dotnet apps/katalog-api/src/Katalog.Api/bin/Debug/net10.0/Katalog.Api.dll --migrate
dotnet apps/katalog-api/src/Katalog.Api/bin/Debug/net10.0/Katalog.Api.dll --rollback 0
```

## Mönster (från arkitekturrapporten)

- `.slnx` + central pakethantering (`Directory.Packages.props`), `TreatWarningsAsErrors` med
  `CS0612;CS0618`-undantag (deprecade Spotify-schemafält bryter inte bygget).
- Build-time OpenAPI-generering till `contracts/`; alla endpoints har `operationId`.
  `appsettings.OpenApiGeneration.json` med placeholder-förbindelser + `OpenApiDocumentGeneration`-guards
  så `dotnet build` fungerar utan levande Postgres/Spotify.
- EF Core 10 + Npgsql: junction-tabeller (`album_artists`, `artist_label`), inga JSON-kolumner,
  enums som `int` med gaps, `Guid.CreateVersion7()`/`uuidv7()`, `spotify_id text unique` +
  idempotent upsert, raw `label_spotify` + normaliserad `label_id` nullable.
- Spotify-resilience: custom pipeline `TotalTimeout → Retry (ShouldRetryAfterHeader) →
  CircuitBreaker → AttemptTimeout`, token-owning delegating handler med 401 → en refresh + retry.
