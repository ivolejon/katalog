import { defineStore } from 'pinia'
import { api } from '@/api'
import type { CreateLabelInput, LabelSummary } from '@/api'

interface LabelsState {
  labels: LabelSummary[]
  loading: boolean
  error: string | null
  /** True once the first fetch has completed (drives skeletons vs empty states). */
  initialized: boolean
}

let running = 0

export const useLabelsStore = defineStore('labels', {
  state: (): LabelsState => ({
    labels: [],
    loading: false,
    error: null,
    initialized: false,
  }),

  getters: {
    isFollowing: (state) => {
      const byKey = new Map<string, LabelSummary>()
      for (const label of state.labels) {
        byKey.set(label.spotifyId, label)
      }
      return (spotifyId: string) => byKey.has(spotifyId)
    },
  },

  actions: {
    async fetchLabels(force = false) {
      if ((this.initialized && !force) || this.loading) {
        return
      }
      this.loading = true
      this.error = null
      const attempt = ++running
      try {
        this.labels = await api.listLabels()
      } catch (err) {
        // A stale in-flight fetch must not clobber a newer result.
        if (attempt === running) {
          this.error = err instanceof Error ? err.message : 'Failed to load labels'
        }
      } finally {
        if (attempt === running) {
          this.loading = false
          this.initialized = true
        }
      }
    },

    async addLabel(input: CreateLabelInput): Promise<LabelSummary> {
      const created = await api.createLabel(input)
      this.labels = [created, ...this.labels.filter((l) => l.id !== created.id)]
      return created
    },

    async removeLabel(id: string): Promise<void> {
      const previous = this.labels
      // Optimistic removal; restore on failure.
      this.labels = previous.filter((l) => l.id !== id)
      try {
        await api.deleteLabel(id)
      } catch (err) {
        this.labels = previous
        throw err
      }
    },
  },
})