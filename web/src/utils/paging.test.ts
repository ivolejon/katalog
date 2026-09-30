import { describe, expect, it } from 'vitest'
import { countUniqueArtists, mergeReleasePages } from './paging'
import type { AlbumSummary } from '@/api'

function release(
  id: string,
  name: string,
  artistNames: string[],
  releaseDate: string | null = '2024-01-01',
): AlbumSummary {
  return {
    id,
    spotifyId: `spotify-${id}`,
    name,
    albumType: 'album',
    releaseDate,
    releaseDatePrecision: 'day',
    imageUrl: null,
    externalUrl: `https://open.spotify.com/album/${id}`,
    totalTracks: 10,
    artistNames,
    artistSpotifyIds: [],
  }
}

describe('mergeReleasePages', () => {
  it('appends a new page without duplicates', () => {
    const existing = [release('a', 'Album A', ['Artist 1'])]
    const page = { releases: [release('b', 'Album B', ['Artist 2'])] }

    const merged = mergeReleasePages(existing, page)

    expect(merged).toHaveLength(2)
    expect(merged.map((r) => r.id)).toEqual(['a', 'b'])
  })

  it('does not double albums when pages overlap', () => {
    const existing = [
      release('a', 'Album A', ['Artist 1']),
      release('b', 'Album B', ['Artist 2']),
    ]
    const page = {
      releases: [
        release('b', 'Album B', ['Artist 2']),
        release('c', 'Album C', ['Artist 3']),
      ],
    }

    const merged = mergeReleasePages(existing, page)

    expect(merged).toHaveLength(3)
    expect(merged.map((r) => r.id)).toEqual(['a', 'b', 'c'])
  })

  it('keeps the already loaded copy when an overlap occurs', () => {
    const existing = [release('a', 'Original A', ['Artist 1'])]
    const page = { releases: [{ ...release('a', 'New A', ['Artist 2']), totalTracks: 99 }] }

    const merged = mergeReleasePages(existing, page)

    expect(merged).toHaveLength(1)
    expect(merged[0].name).toBe('Original A')
    expect(merged[0].artistNames).toEqual(['Artist 1'])
  })
})

describe('countUniqueArtists', () => {
  it('counts two albums by the same artist once', () => {
    const releases = [
      { ...release('a', 'Album A', ['Karin Dreijer']), artistSpotifyIds: ['artist-1'] },
      { ...release('b', 'Album B', ['Karin Dreijer']), artistSpotifyIds: ['artist-1'] },
    ]

    expect(countUniqueArtists(releases)).toBe(1)
  })

  it('counts two different artists who share a name as two', () => {
    const releases = [
      { ...release('a', 'Album A', ['Karin Dreijer']), artistSpotifyIds: ['artist-1'] },
      { ...release('b', 'Album B', ['Karin Dreijer']), artistSpotifyIds: ['artist-2'] },
    ]

    expect(countUniqueArtists(releases)).toBe(2)
  })

  it('does not double-count a repeated artist across pages', () => {
    const releases = [
      { ...release('a', 'Album A', ['Karin Dreijer']), artistSpotifyIds: ['artist-1'] },
      { ...release('b', 'Album B', ['Fever Ray']), artistSpotifyIds: ['artist-2'] },
      { ...release('c', 'Album C', ['Karin Dreijer']), artistSpotifyIds: ['artist-1'] },
    ]

    expect(countUniqueArtists(releases)).toBe(2)
  })

  it('falls back to artist names when releases carry no Spotify ids', () => {
    const releases = [
      release('a', 'Album A', ['Karin Dreijer']),
      release('b', 'Album B', ['Fever Ray']),
    ]

    expect(countUniqueArtists(releases)).toBe(2)
  })

  it('returns zero for an empty list', () => {
    expect(countUniqueArtists([])).toBe(0)
  })
})
