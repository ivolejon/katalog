import { http } from './http'
import type {
  CreateLabelInput,
  LabelDetail,
  LabelSearchResponse,
  LabelSummary,
} from './types'

/**
 * Endpoint surface agreed with the backend (arch report 3.2 / firstmate spec).
 * Handwritten until the OpenAPI contract generates these; see src/api/README.md.
 */

export interface SearchLabelsParams {
  q: string
  limit?: number
}

export const api = {
  /** List all followed labels. */
  listLabels(): Promise<LabelSummary[]> {
    return http.get<LabelSummary[]>('/labels')
  },

  /** Add a label to follow. */
  createLabel(input: CreateLabelInput): Promise<LabelSummary> {
    return http.post<LabelSummary>('/labels', input)
  },

  /** Stop following a label. */
  deleteLabel(id: string): Promise<void> {
    return http.delete<void>(`/labels/${id}`)
  },

  /** Full label detail with artists and releases. */
  getLabel(id: string): Promise<LabelDetail> {
    return http.get<LabelDetail>(`/labels/${id}`)
  },

  /** Spotify label search (primary add-label flow): albums matching the label name. */
  searchLabels(params: SearchLabelsParams): Promise<LabelSearchResponse> {
    const query = new URLSearchParams({ q: params.q })
    if (params.limit !== undefined) {
      query.set('limit', String(params.limit))
    }
    return http.get<LabelSearchResponse>(`/labels/search?${query.toString()}`)
  },
}
