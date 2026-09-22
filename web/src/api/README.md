# src/api - Katalog API client

## Status: handwritten, contract available

The backend generates and commits `contracts/katalog-api/openapi.json`. Until
the generated client is adopted, `src/api/` ships a **handwritten typed client**
matching the contract's endpoint surface (arch report 3.6):

| Endpoint | Client function |
|---|---|
| `GET /api/labels` | `api.listLabels()` |
| `POST /api/labels` | `api.createLabel(input)` |
| `DELETE /api/labels/{id}` | `api.deleteLabel(id)` |
| `GET /api/labels/{id}` | `api.getLabel(id)` |
| `GET /api/labels/search` | `api.searchLabels({ q })` |
| `GET /api/search?type=artist` | `api.searchArtists({ q })` |

## Switching to the generated client

To adopt the committed contract as a generated client, run:

```sh
npm run generate:client
```

That runs `@hey-api/openapi-ts` and writes types + client functions to
`src/api/generated/`. The generated `sdk.gen.ts` / `types.gen.ts` then replace
`http.ts` + `types.ts`; swap `api/labels.ts` to call the generated functions and
delete this file's handwritten-client note. Keep the `/api` base path and error
handling consistent so views and stores do not change.

`API_CLIENT_ORIGIN` in `index.ts` tracks which client is active for the PR
review and for the no-mistakes gate.
