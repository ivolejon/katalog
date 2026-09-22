import { defineStore } from 'pinia'
import { api } from '@/api'
import type { CreateLabelInput, LabelSummary } from '@/api'

interface LabelsState {
  labels: LabelSummary[]
  loading: boolean
  error: string | null
  /** True once the first fetch has completed (drives skeletons vs empty states). */
  initialized: boolean
  fetchId: number
  revision: number
}

export const useLabelsStore = defineStore('labels', {
  state: (): LabelsState => ({
    labels: [],
    loading: false,
    error: null,
    initialized: false,
    fetchId: 0,
    revision: 0,
  }),

  getters: {
    isFollowing: (state) => {
      const byKey = new Set<string>()
      for (const label of state.labels) {
        for (const spotifyId of label.spotifyIds) {
          byKey.add(spotifyId)
        }
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
      const fetchId = ++this.fetchId
      const revision = this.revision
      try {
        const labels = await api.listLabels()
        if (fetchId === this.fetchId && revision === this.revision) {
          this.labels = labels
        }
      } catch (err) {
        if (fetchId === this.fetchId && revision === this.revision) {
          this.error = err instanceof Error ? err.message : 'Failed to load labels'
        }
      } finally {
        if (fetchId === this.fetchId) {
          this.loading = false
          this.initialized = true
        }
      }
    },

    async addLabel(input: CreateLabelInput): Promise<LabelSummary> {
      const created = await api.createLabel(input)
      this.revision += 1
      this.labels = [created, ...this.labels.filter((l) => l.id !== created.id)]
      return created
    },

    async removeLabel(id: string): Promise<void> {
      const previous = this.labels.find((label) => label.id === id)
      this.revision += 1
      // Optimistic removal; restore on failure.
      this.labels = this.labels.filter((l) => l.id !== id)
      try {
        await api.deleteLabel(id)
      } catch (err) {
        if (previous && !this.labels.some((label) => label.id === id)) {
          this.labels = [...this.labels, previous]
        }
        throw err
      }
    },
  },
})
