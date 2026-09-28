import { computed, ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import { api } from '@/api'
import type { LabelSearchResponse, LabelSearchResult } from '@/api'

/**
 * Debounced Spotify label search (GET /api/labels/search), backing the add-label
 * dialog. Empty queries reset without hitting the API.
 */
export function useLabelSearch(debounceMs = 350) {
  const query = ref('')
  const response = ref<LabelSearchResponse | null>(null)
  const searching = ref(false)
  const error = ref<string | null>(null)

  const labels = computed(() => response.value?.labels ?? [])

  let requestSeq = 0

  const run = useDebounceFn(async (q: string) => {
    const seq = ++requestSeq
    searching.value = true
    error.value = null
    try {
      const data = await api.searchLabels({ q, limit: 10 })
      if (seq === requestSeq) {
        response.value = data
      }
    } catch (err) {
      if (seq === requestSeq) {
        error.value = err instanceof Error ? err.message : 'Label search failed'
        response.value = null
      }
    } finally {
      if (seq === requestSeq) {
        searching.value = false
      }
    }
  }, debounceMs)

  watch(query, (value) => {
    if (!value.trim()) {
      requestSeq += 1
      run.cancel()
      response.value = null
      searching.value = false
      error.value = null
      return
    }
    requestSeq += 1
    run(value.trim())
  })

  return { query, labels, searching, error, reset: () => (query.value = '') }
}

/** Collect unique Spotify artist ids from every album under a label search result. */
export function collectArtistIds(label: LabelSearchResult): string[] {
  const ids = new Set<string>()
  for (const album of label.albums) {
    for (const artist of album.artists) {
      ids.add(artist.spotifyId)
    }
  }
  return [...ids]
}
