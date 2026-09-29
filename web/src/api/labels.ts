import { http } from './http'
import type {
  CreateLabelInput,
  LabelDetail,
  LabelReleasesPage,
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

  /**
   * Full label detail with artists and counters.
   * Pass includeReleases=false when releases are loaded page-by-page via getLabelReleases.
   */
  getLabel(id: string, includeReleases = true): Promise<LabelDetail> {
    const query = new URLSearchParams()
    if (!includeReleases) {
      query.set('includeReleases', 'false')
    }
    const suffix = query.toString() ? `?${query.toString()}` : ''
    return http.get<LabelDetail>(`/labels/${id}${suffix}`)
  },

  /** Paged releases for a label. No Spotify calls are made per page. */
  getLabelReleases(id: string, page: number, pageSize: number): Promise<LabelReleasesPage> {
    const query = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    })
    return http.get<LabelReleasesPage>(`/labels/${id}/releases?${query.toString()}`)
  },

  /** Spotify label search (primary add-label flow): label hits matching the query. */
  searchLabels(params: SearchLabelsParams): Promise<LabelSearchResponse> {
    const query = new URLSearchParams({ q: params.q })
    if (params.limit !== undefined) {
      query.set('limit', String(params.limit))
    }
    return http.get<LabelSearchResponse>(`/labels/search?${query.toString()}`)
  },
}
