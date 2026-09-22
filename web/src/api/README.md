# src/api - Katalog API client

## Status: handwritten, awaiting the OpenAPI contract

The backend worker generates `contracts/katalog-api/openapi.json` and commits it
to the repo root. Until that file exists on this branch, `src/api/` ships a
**handwritten typed client** matching the agreed endpoint surface
(arch report 3.6):

| Endpoint | Client function |
|---|---|
| `GET /api/labels` | `api.listLabels()` |
| `POST /api/labels` | `api.createLabel(input)` |
| `DELETE /api/labels/{id}` | `api.deleteLabel(id)` |
| `GET /api/labels/{id}` | `api.getLabel(id)` |
| `GET /api/search?type=artist` | `api.searchArtists({ q })` |

## Switching to the generated client

When `../contracts/katalog-api/openapi.json` exists, run:

```sh
npm run generate:client
```

That runs `@hey-api/openapi-ts` and writes types + client functions to
`src/api/generated/`. The generated `sdk.gen.ts` / `types.gen.ts` then replace
`http.ts` + `types.ts`; swap `api/labels.ts` to call the generated functions and
delete this file's "handwritten" note. Keep the `/api` base path and error
handling consistent so views and stores do not change.

`API_CLIENT_ORIGIN` in `index.ts` tracks which client is active for the PR
review and for the no-mistakes gate.