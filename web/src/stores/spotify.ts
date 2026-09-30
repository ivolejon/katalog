import { defineStore } from 'pinia'
import { toast } from 'vue-sonner'
import { ApiError, spotifyApi } from '@/api'
import type { SpotifyDevice, SpotifyPlaybackState, SpotifySession } from '@/api'

const DEVICE_STORAGE_KEY = 'katalog.spotify.deviceId'
const LOGIN_URL = '/api/spotify/auth/login'

/** How long a cached device list stays fresh (Spotify device ids are not permanent). */
const DEVICE_CACHE_MS = 60_000

interface SpotifyState {
  session: SpotifySession | null
  devices: SpotifyDevice[]
  /** The device playback commands are sent to; defaults to Spotify's active device. */
  selectedDeviceId: string | null
  playback: SpotifyPlaybackState | null
  devicesLoading: boolean
  devicesError: string | null
  /** Release currently being sent to Spotify, so only that card shows a spinner. */
  busyAlbumId: string | null
  initialized: boolean
  devicesFetchedAt: number
}

/**
 * Spotify Connect state: who is signed in, which device playback goes to, and what is playing.
 *
 * The device list is fetched on demand and cached client-side (device ids expire, so it is
 * refetched after a minute or when the list is empty). Playback state is fetched once per
 * device/connect change and then updated locally from the result of each play/pause, so a
 * release card never triggers a poll of its own.
 */
export const useSpotifyStore = defineStore('spotify', {
  state: (): SpotifyState => ({
    session: null,
    devices: [],
    selectedDeviceId: readStoredDeviceId(),
    playback: null,
    devicesLoading: false,
    devicesError: null,
    busyAlbumId: null,
    initialized: false,
    devicesFetchedAt: 0,
  }),

  getters: {
    isConnected: (state) => state.session?.isConnected === true,
    hasPremium: (state) => state.session?.product === 'premium',
    /** Devices Spotify will accept commands on. */
    availableDevices: (state) => state.devices.filter((device) => !device.isRestricted),
    activeDevice(state): SpotifyDevice | null {
      const activeId = state.devices.find((d) => d.isActive)?.id
      if (activeId) {
        return state.devices.find((d) => d.id === activeId) ?? null
      }
      return state.devices.find((d) => !d.isRestricted) ?? null
    },
    /** The device playback is aimed at, falling back to Spotify's active device. */
    targetDeviceId(state): string | null {
      if (state.selectedDeviceId && state.devices.some((d) => d.id === state.selectedDeviceId)) {
        return state.selectedDeviceId
      }
      return state.devices.find((d) => d.isActive && !d.isRestricted)?.id ?? null
    },
  },

  actions: {
    /** Reads the sign-in status once per page load. */
    async loadSession(force = false) {
      if (this.initialized && !force) {
        return
      }
      try {
        this.session = await spotifyApi.session()
      } catch {
        // A backend that is not running must not break the page; the play buttons simply
        // stay in their signed-out state.
        this.session = { isConnected: false, spotifyUserId: null, displayName: null, product: null }
      } finally {
        this.initialized = true
      }

      if (this.isConnected) {
        await this.loadDevices()
      }
    },

    /**
     * Handles the outcome the OAuth callback appends to the app URL
     * (?spotify=connected|failed) and cleans the query string.
     */
    async consumeLoginCallback(): Promise<void> {
      const params = new URLSearchParams(window.location.search)
      const outcome = params.get('spotify')
      if (!outcome) {
        return
      }

      const reason = params.get('reason')
      params.delete('spotify')
      params.delete('reason')
      const query = params.toString()
      window.history.replaceState({}, '', `${window.location.pathname}${query ? `?${query}` : ''}`)

      this.initialized = false
      await this.loadSession(true)

      if (outcome === 'connected') {
        toast.success('Signed in with Spotify', {
          description: this.session?.displayName
            ? `Playing on your devices as ${this.session.displayName}.`
            : 'You can now play releases on your own devices.',
        })
      } else {
        toast.error('Spotify sign-in failed', {
          description: loginFailureText(reason),
        })
      }
    },

    /** Sends the browser to the backend, which redirects to Spotify's consent screen. */
    login() {
      window.location.assign(LOGIN_URL)
    },

    async logout() {
      try {
        await spotifyApi.logout()
      } finally {
        this.session = { isConnected: false, spotifyUserId: null, displayName: null, product: null }
        this.devices = []
        this.playback = null
        this.devicesFetchedAt = 0
      }
    },

    /** Loads the device list, from cache unless it is stale or was never fetched. */
    async loadDevices(force = false) {
      if (!this.isConnected) {
        this.devices = []
        return
      }
      if (!force && this.devices.length > 0 && Date.now() - this.devicesFetchedAt < DEVICE_CACHE_MS) {
        return
      }

      this.devicesLoading = true
      this.devicesError = null
      try {
        const result = await spotifyApi.devices()
        this.devices = result.devices
        this.devicesFetchedAt = Date.now()
        this.pruneSelectedDevice()
        await this.loadPlaybackState()
      } catch (err) {
        this.devicesError = describeError(err, 'Could not load your Spotify devices')
        this.handleSessionError(err)
      } finally {
        this.devicesLoading = false
      }
    },

    /** Fetches what is playing, so the toggle starts from the truth. */
    async loadPlaybackState() {
      if (!this.isConnected) {
        this.playback = null
        return
      }
      try {
        this.playback = await spotifyApi.playbackState()
      } catch (err) {
        this.handleSessionError(err)
      }
    },

    selectDevice(deviceId: string | null) {
      this.selectedDeviceId = deviceId
      if (deviceId) {
        window.localStorage.setItem(DEVICE_STORAGE_KEY, deviceId)
      } else {
        window.localStorage.removeItem(DEVICE_STORAGE_KEY)
      }
    },

    /** True when this release is the context currently playing on the target device. */
    isPlayingAlbum(spotifyAlbumId: string): boolean {
      return this.playback?.isPlaying === true && this.playback.contextUri === `spotify:album:${spotifyAlbumId}`
    },

    /**
     * Play/pause toggle for a release: plays the album as the playback context, or pauses it
     * when that album is what is already playing.
     */
    async toggleAlbum(spotifyAlbumId: string): Promise<'played' | 'paused'> {
      if (!this.isConnected) {
        this.login()
        return 'played'
      }

      const deviceId = this.targetDeviceId
      this.busyAlbumId = spotifyAlbumId
      try {
        if (this.isPlayingAlbum(spotifyAlbumId)) {
          await spotifyApi.pause(deviceId)
          this.playback = { ...(this.playback ?? emptyPlayback()), isPlaying: false, deviceId }
          return 'paused'
        }

        await spotifyApi.play(spotifyAlbumId, deviceId)
        this.playback = {
          isPlaying: true,
          contextUri: `spotify:album:${spotifyAlbumId}`,
          contextType: 'album',
          deviceId,
        }
        return 'played'
      } catch (err) {
        toast.error(playbackErrorTitle(err), { description: describeError(err, 'Spotify could not start playback') })
        this.handleSessionError(err)
        // A missing device is the one failure we can act on: refresh the list so the picker
        // shows the device that just became active (or that there is none).
        if (err instanceof ApiError && err.status === 404) {
          await this.loadDevices(true)
        }
        return 'played'
      } finally {
        this.busyAlbumId = null
      }
    },

    /** A dead session leaves the UI in its signed-out state instead of erroring forever. */
    handleSessionError(err: unknown) {
      if (err instanceof ApiError && err.status === 401) {
        this.session = { isConnected: false, spotifyUserId: null, displayName: null, product: null }
        this.devices = []
        this.playback = null
      }
    },

    /** Drops a remembered device that is no longer in the list. */
    pruneSelectedDevice() {
      if (this.selectedDeviceId && !this.devices.some((d) => d.id === this.selectedDeviceId)) {
        this.selectDevice(null)
      }
    },
  },
})

function emptyPlayback(): SpotifyPlaybackState {
  return { isPlaying: false, contextUri: null, contextType: null, deviceId: null }
}

function readStoredDeviceId(): string | null {
  try {
    return window.localStorage.getItem(DEVICE_STORAGE_KEY)
  } catch {
    return null
  }
}

/**
 * Prefers the API's problem details (written to be read by a human) and falls back to a
 * generic line. A 404 on a player call is almost always "no active device", which the user
 * can act on, so it gets a concrete instruction instead of an empty toast.
 */
function describeError(err: unknown, fallback: string): string {
  if (!(err instanceof ApiError)) {
    return err instanceof Error ? err.message : fallback
  }
  if (err.status === 404) {
    return 'No active Spotify device. Open the Spotify app on the device you want to hear.'
  }
  return err.detail ?? err.message ?? fallback
}

function playbackErrorTitle(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.status === 403) {
      return 'Spotify Premium required'
    }
    if (err.status === 404) {
      return 'No active Spotify device'
    }
    if (err.status === 401) {
      return 'Your Spotify session expired'
    }
  }
  return 'Playback failed'
}

function loginFailureText(reason: string | null): string {
  switch (reason) {
    case 'access_denied':
      return 'You cancelled the Spotify sign-in.'
    case 'state_mismatch':
    case 'no_sign_in_attempt':
      return 'That sign-in link was stale or could not be verified. Try signing in again.'
    default:
      return 'Spotify did not complete the sign-in. Try again.'
  }
}
