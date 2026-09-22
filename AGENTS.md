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

## Maintaining this file

Keep this file for knowledge useful to almost every future agent session in this project.
Do not repeat what the codebase already shows; point to the authoritative file or command instead.
Prefer rewriting or pruning existing entries over appending new ones.
When updating this file, preserve this bar for all agents and keep entries concise.
