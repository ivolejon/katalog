# web - Katalog frontend

Vue 3 + TypeScript SPA built with [shadcn-vue](https://shadcn-vue.com/) using
the **a1AhVxI** preset (reka-ui base, Hugeicons, Figtree, neutral base color,
Tailwind CSS 4, "maia" visual style).

Tracks record labels through the Spotify catalog for a single user - no login
in the MVP (arch report 3.3).

## Stack

- Vue 3.5 + TypeScript 5.9 + Vite 8 (research 4.1)
- Tailwind CSS 4 via `@tailwindcss/vite`, CSS-first config in `src/assets/index.css`
- shadcn-vue components in `src/components/ui/` (installed via `npx shadcn-vue add`)
- `vue-router` (lazy routes) + `pinia` for app state; data fetching in composables
  (TanStack Query intentionally not added - Pinia + composables covers the MVP surface)
- `vue-sonner` for toasts

## Getting started

```sh
npm install
npm run dev        # http://localhost:5173
npm run typecheck  # vue-tsc
npm run build      # typecheck + production build
```

### Talking to the backend

The app always calls relative `/api/*` paths. In dev, `vite.config.ts` proxies
`/api` to the backend:

1. `API_HTTP` / `API_HTTPS` env vars (injected by Aspire when the app runs as
   an `AddViteApp` resource)
2. fallback: `http://localhost:5192` (local API default)

Start the backend with `aspire run` or point the fallback at whatever port the
API runs on.

## API client - handwritten, awaiting the OpenAPI contract

The backend generates and commits `contracts/katalog-api/openapi.json`. Until
that file lands, `src/api/` ships a handwritten typed client matching the
agreed surface (`src/api/README.md` documents the endpoint map and how to
switch to the generated client):

```sh
npm run generate:client   # @hey-api/openapi-ts -> src/api/generated
```

`src/api/index.ts` exports `API_CLIENT_ORIGIN` so the PR can state exactly what
is handwritten vs generated.

## AppHost integration

`AddViteApp` wiring in `Katalog.AppHost` is owned by the backend worker and was
not present at the start of this branch, so it will land in a later commit
(after `Katalog.AppHost/Program.cs` exists, add `AddViteApp("web", "../web")`
per research 4.2).

## UI kit notes

- To re-add or upgrade shadcn-vue components: `npx shadcn-vue@latest add <name>`.
- **Icon shim**: the CLI generates components that import named icons from
  `@hugeicons/vue`, but that package only exports the generic `HugeiconsIcon`
  component. `src/lib/icons.ts` bridges this (wraps `@hugeicons/core-free-icons`
  data). When a re-add pulls in a new icon, extend `src/lib/icons.ts` with the
  new name.
- The emitted UI components referencing icons import from `@/lib/icons` instead
  of `@hugeicons/vue`.