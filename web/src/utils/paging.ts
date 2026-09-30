import type { AlbumSummary } from '@/api'

/**
 * Merge a freshly fetched page into the already loaded releases.
 *
 * Releases are keyed by id so that overlapping pages (caused by new releases
 * being discovered in the background and inserted at the top of the list)
 * never render the same album twice.
 */
export function mergeReleasePages(
  existing: AlbumSummary[],
  page: { releases: AlbumSummary[] },
): AlbumSummary[] {
  const seen = new Set(existing.map((r) => r.id))
  return [...existing, ...page.releases.filter((r) => !seen.has(r.id))]
}

/** Count distinct artists across a set of releases, keyed by Spotify id. */
export function countUniqueArtists(releases: AlbumSummary[]): number {
  const ids = new Set<string>()
  for (const release of releases) {
    ids.add(release.spotifyId)
  }
  return ids.size
}
