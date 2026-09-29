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

/** Count distinct artist names across a set of releases. */
export function countUniqueArtists(releases: AlbumSummary[]): number {
  const names = new Set<string>()
  for (const release of releases) {
    for (const name of release.artistNames) {
      names.add(name)
    }
  }
  return names.size
}
