# Katalog - Workflows

## Frontend development

```sh
cd web
npm install
npm run dev              # Vite dev server, http://localhost:5173
npm run typecheck        # vue-tsc -b --noEmit
npm run build            # typecheck + production build
npm run generate:client  # @hey-api/openapi-ts -> src/api/generated (once
                         # contracts/katalog-api/openapi.json exists)
```

The dev server proxies `/api` to the backend on `API_HTTP`/`API_HTTPS`
(Aspire-injected) or falls back to `http://localhost:5192`.

## CI

`.github/workflows/web.yml` runs on pull requests to `main`: installs
`web/` dependencies, builds and typechecks. When the backend lands, its own
workflow adds .NET build + tests + contract-drift checks.

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
- Frontend and backend currently moved as independent PRs; the contract
  (`contracts/katalog-api/openapi.json`) is the coupling point - the frontend
  client is regenerated when it appears.
- Merge authority: the captain approves every merge (no autonomous merge).