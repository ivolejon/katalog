import { ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import { api } from '@/api'
import type { ArtistSearchResult } from '@/api'

/**
 * Debounced Spotify artist search, backing the add-label combobox.
 * Empty queries reset without hitting the API.
 */
export function useArtistSearch(debounceMs = 350) {
  const query = ref('')
  const results = ref<ArtistSearchResult[]>([])
  const searching = ref(false)
  const error = ref<string | null>(null)

  let requestSeq = 0

  const run = useDebounceFn(async (q: string) => {
    const seq = ++requestSeq
    searching.value = true
    error.value = null
    try {
      const data = await api.searchArtists({ q, limit: 12 })
      if (seq === requestSeq) {
        results.value = data
      }
    } catch (err) {
      if (seq === requestSeq) {
        error.value = err instanceof Error ? err.message : 'Artist search failed'
        results.value = []
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
      results.value = []
      searching.value = false
      error.value = null
      return
    }
    run(value.trim())
  })

  return { query, results, searching, error, reset: () => (query.value = '') }
}