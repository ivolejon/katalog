/**
 * Thin typed fetch wrapper for the Katalog API.
 *
 * All calls go to the relative `/api` base so the Vite dev proxy (see
 * vite.config.ts) can forward them to the backend, and so static hosting can
 * proxy the same path in production (arch report 4.2).
 *
 * Handwritten until the OpenAPI-generated client replaces it.
 */

export class ApiError extends Error {
  readonly status: number
  readonly detail: string | null

  constructor(status: number, message: string, detail: string | null = null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
  }
}

interface ApiProblem {
  title?: string
  status?: number
  detail?: string
}

const DEFAULT_HEADERS = { Accept: 'application/json' }

async function parseErrorBody(response: Response): Promise<ApiProblem> {
  try {
    return (await response.json()) as ApiProblem
  } catch {
    return {}
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: {
      ...DEFAULT_HEADERS,
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    const problem = await parseErrorBody(response)
    throw new ApiError(
      response.status,
      problem.title ?? `Request failed with status ${response.status}`,
      problem.detail ?? null,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const http = {
  get<T>(path: string): Promise<T> {
    return request<T>(path)
  },
  post<T>(path: string, body?: unknown): Promise<T> {
    return request<T>(path, {
      method: 'POST',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  },
  delete<T>(path: string): Promise<T> {
    return request<T>(path, { method: 'DELETE' })
  },
}