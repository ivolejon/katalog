/**
 * Handwritten API DTO types.
 *
 * These mirror the committed contract surface (arch report 3.6): labels CRUD,
 * label search, label detail with artists + releases. They remain a stand-in
 * until `npm run generate:client` (hey-api) emits `src/api/generated/*` and the
 * generated types replace these ones.
 */

/** App-owned followed label, summary form (GET /api/labels). */
export interface LabelSummary {
  id: string
  /** Spotify artist ids currently linked to this label. */
  spotifyIds: string[]
  name: string
  /** Number of artists currently linked to this label. */
  artistCount: number
  createdAt: string
}

/** A label as it can be added (POST /api/labels). */
export interface CreateLabelInput {
  name: string
  /** Spotify artist ids linked to this label (polling anchors). */
  spotifyIds: string[]
}

/** Artist on a label-search album hit (simplified: id + name from the album object). */
export interface LabelSearchArtist {
  spotifyId: string
  name: string
}

/** Album hit from a Spotify label search (GET /api/labels/search). */
export interface LabelSearchAlbum {
  albumId: string
  name: string
  artists: LabelSearchArtist[]
  imageUrl: string | null
  releaseDate: string | null
  externalUrl: string
}

/** A label hit from a Spotify label search. */
export interface LabelSearchResult {
  name: string
  albums: LabelSearchAlbum[]
}

/** Label search response: the query plus the matching label hits. */
export interface LabelSearchResponse {
  query: string
  labels: LabelSearchResult[]
}

export type ReleaseDatePrecision = 'year' | 'month' | 'day'
export type AlbumType = 'album' | 'single' | 'compilation'

/** Album, summary form used in the label detail release feed. */
export interface AlbumSummary {
  id: string
  spotifyId: string
  name: string
  albumType: AlbumType
  releaseDate: string | null
  releaseDatePrecision: ReleaseDatePrecision
  imageUrl: string | null
  externalUrl: string
  totalTracks: number
  /** Artist names credited on the release, ordered alphabetically. */
  artistNames: string[]
  /** Spotify ids of the artists credited on the release. */
  artistSpotifyIds: string[]
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
  spotifyIds: string[]
  name: string
  artistCount: number
  releaseCount: number
  createdAt: string
  artists: LabelArtist[]
  releases: AlbumSummary[]
}

/** A page of releases for a label (GET /api/labels/{id}/releases). */
export interface LabelReleasesPage {
  page: number
  pageSize: number
  totalCount: number
  hasMore: boolean
  releases: AlbumSummary[]
}
