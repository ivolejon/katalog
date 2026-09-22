---
type: "Reference"
title: "Katalog - Testing"
openwiki_generated: true
verified:
  - by: openwiki/0.5.2
    at: 2026-09-22T17:28:24.726Z
sources:
  - id: openwiki-source-164e2da859b5277df81c7d94
    resource: repo://.github/workflows/ci.yml
  - id: openwiki-source-d23c93c81dff4d6aec30e9fb
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/AppHost/AppHostSmokeTests.cs
  - id: openwiki-source-064c36b36867a42c9247c3ba
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Integration/LabelsApiIntegrationTests.cs
  - id: openwiki-source-7796feddb7c97ca8d4140109
    resource: repo://apps/katalog-api/tests/Katalog.Api.Tests/Katalog.Api.Tests.csproj
generated: { by: "opencode", at: "2026-09-22T17:28:24.726Z" }
---

# Katalog - Testing

## What is verified today

- **Typecheck + build** - `vue-tsc -b` and `vite build` pass in CI
  (`.github/workflows/web.yml`) and locally.
- **Live smoke (best effort)** - the initial frontend implementation was
  verified against a mock API in headless Chromium: the app serves, labels
  list/detail and add-label-by-artist-search render, and the release tabs
  show data.
- **Planned frontend scenarios** - no browser test files currently record live
  verification for these scenarios. They are intended for a future integration
  phase against the real backend stack:
  add label via Spotify artist search; dialog state reset on cancel/reopen;
  debounce (stale search results do not repopulate an emptied query); fast
  navigation between label details; concurrent mutation vs refresh.
- The store's stale-response guards (`fetchId`/`revision`) and the debounced
  search composable are deliberately structured so those scenarios can be
  asserted with minimal test infrastructure.

## Backend test strategy

- xUnit v3 in `apps/katalog-api/tests/Katalog.Api.Tests`:
  - unit: poll logic (cursor, dedupe/upsert), token provider with
    `FakeTimeProvider`;
  - integration: `WebApplicationFactory<Program>` +
    `Testcontainers.PostgreSql` (EF migrations/upsert), Spotify mocked with
    **WireMock** including 429/`Retry-After` scenarios;
  - AppHost smoke: `DistributedApplicationTestingBuilder` asserting resources
    exist and `/health` responds.
- Naming convention `Method_Condition_Expected` with display names.
- Interactive/e2e smoke against the full stack (Aspire + Postgres + WireMock or
  real Spotify) complements these automated tests and the frontend scenarios.

## Rules

- Never run poll tests against the real Spotify API; always stub/mock.
- Never answer no-mistakes ask-user findings yourself - escalate to firstmate,
  and never pass `--yes` to the gate.
