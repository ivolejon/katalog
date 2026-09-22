/**
 * Handwritten API DTO types.
 *
 * These mirror the agreed contract surface (arch report 3.6): labels CRUD,
 * artist search, label detail with artists + releases. They are a stand-in
 * until `contracts/katalog-api/openapi.json` lands; then
 * `npm run generate:client` (hey-api) emits `src/api/generated/*` and these
 * types are replaced by the generated ones.
 */

/** App-owned followed label, summary form (GET /api/labels). */
export interface LabelSummary {
  id: string
  /** Spotify artist/label id the follows entry points at. */
  spotifyId: string
  name: string
  /** Number of artists currently linked to this label. */
  artistCount: number
  createdAt: string
}

/** A label as it can be added (POST /api/labels). */
export interface CreateLabelInput {
  spotifyId: string
  name: string
}

/** Artist search result (GET /api/search?type=artist). */
export interface ArtistSearchResult {
  id: string
  name: string
  imageUrl: string | null
  externalUrl: string
  genres: string[]
}

export type ReleaseDatePrecision = 'year' | 'month' | 'day'
export type AlbumType = 'album' | 'single' | 'compilation'

/** Album, summary form used in the label detail release feed. */
export interface AlbumSummary {
  id: string
  spotifyId: string
  name: string
  albumType: AlbumType
  releaseDate: string
  releaseDatePrecision: ReleaseDatePrecision
  imageUrl: string | null
  externalUrl: string
  totalTracks: number
}

/** Artist linked to a label, detail form (GET /api/labels/{id}). */
export interface LabelArtist {
  id: string
  spotifyId: string
  name: string
  imageUrl: string | null
  externalUrl: string
  genres: string[] | null
  popularity: number | null
}

/** Full label detail (GET /api/labels/{id}). */
export interface LabelDetail {
  id: string
  spotifyId: string
  name: string
  artistCount: number
  createdAt: string
  artists: LabelArtist[]
  releases: AlbumSummary[]
}
