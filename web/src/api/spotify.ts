import { http } from './http'
import type {
  SpotifyDevices,
  SpotifyPlaybackState,
  SpotifySession,
} from './types'

/**
 * Spotify Connect endpoints (see the backend's /api/spotify group). The sign-in itself is a
 * redirect to the backend's /api/spotify/auth/login - the Spotify app's client id and secret
 * stay on the server, and no token is ever handed to the browser.
 */
export const spotifyApi = {
  /** Whether a Spotify account is connected for this browser. */
  session(): Promise<SpotifySession> {
    return http.get<SpotifySession>('/spotify/me')
  },

  /** The connected account's devices, with the active one flagged. */
  devices(): Promise<SpotifyDevices> {
    return http.get<SpotifyDevices>('/spotify/devices')
  },

  /** What is playing now, so a toggle can show the truth. */
  playbackState(): Promise<SpotifyPlaybackState> {
    return http.get<SpotifyPlaybackState>('/spotify/playback')
  },

  /** Play a release (by its Spotify album id) on the chosen device. */
  play(spotifyAlbumId: string, deviceId: string | null): Promise<void> {
    return http.put<void>('/spotify/playback/play', { spotifyAlbumId, deviceId })
  },

  /** Pause playback on the chosen device. */
  pause(deviceId: string | null): Promise<void> {
    return http.put<void>('/spotify/playback/pause', { deviceId })
  },

  /** Forget the stored Spotify sign-in. */
  logout(): Promise<void> {
    return http.post<void>('/spotify/auth/logout')
  },
}
