# Katalog - Testing

## What is verified today (frontend)

- **Typecheck + build** - `vue-tsc -b` and `vite build` pass in CI
  (`.github/workflows/web.yml`) and locally.
- **Live smoke (best effort)** - the initial frontend implementation was
  verified against a mock API in headless Chromium: the app serves, labels
  list/detail and add-label-by-artist-search render, and the release tabs
  show data.
- **Gate test findings** - the no-mistakes test gate could not run interactive
  scenarios live (no browser driver, no backend in the frontend branch). Per
  the recorded decision, the five interactive scenarios are verified in the
  **integration phase** against the real backend stack:
  add label via Spotify artist search; dialog state reset on cancel/reopen;
  debounce (stale search results do not repopulate an emptied query); fast
  navigation between label details; concurrent mutation vs refresh.
- The store's stale-response guards (`fetchId`/`revision`) and the debounced
  search composable are deliberately structured so those scenarios can be
  asserted with minimal test infrastructure once the backend exists.

## Planned backend strategy (per architecture report)

- xUnit v3 in `apps/katalog-api/tests/Katalog.Api.Tests`:
  - unit: poll logic (cursor, dedupe/upsert), token provider with
    `FakeTimeProvider`;
  - integration: `WebApplicationFactory<Program>` +
    `Testcontainers.PostgreSql` (EF migrations/upsert), Spotify mocked with
    **WireMock** including 429/`Retry-After` scenarios;
  - AppHost smoke: `DistributedApplicationTestingBuilder` asserting resources
    exist and `/health` responds.
- Naming convention `Method_Condition_Expected` with display names.
- Interactive/e2e smoke against the full stack (Aspire + Postgres + WireMock
  or real Spotify) when the backend lands - this supersedes the gate's
  deferred scenarios.

## Rules

- Never run poll tests against the real Spotify API; always stub/mock.
- Never answer no-mistakes ask-user findings yourself - escalate to firstmate,
  and never pass `--yes` to the gate.