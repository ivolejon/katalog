<script setup lang="ts">
import { computed } from 'vue'
import { Button } from '@/components/ui/button'
import { useSpotifyStore } from '@/stores/spotify'
import { Loading03Icon, PauseIcon, PlayIcon, SpotifyIcon } from '@/lib/icons'

const props = withDefaults(
  defineProps<{
    /** The release's Spotify album id; playback is started as this album's context. */
    spotifyAlbumId: string
    /** Release name, used for the button label and the announcement. */
    albumName?: string
    variant?: 'default' | 'outline' | 'ghost'
    size?: 'sm' | 'default' | 'icon'
  }>(),
  { albumName: '', variant: 'outline', size: 'sm' },
)

const store = useSpotifyStore()

const connected = computed(() => store.isConnected)
const playing = computed(() => store.isPlayingAlbum(props.spotifyAlbumId))
const busy = computed(() => store.busyAlbumId === props.spotifyAlbumId)
const label = computed(() => {
  if (!connected.value) {
    return 'Play on Spotify'
  }
  return playing.value ? 'Pause' : 'Play on Spotify'
})

/**
 * Signed out, the click takes the user straight into the Spotify sign-in - the sign-in
 * redirect is the backend's, so no Spotify credential ever reaches this app.
 */
async function toggle() {
  if (busy.value) {
    return
  }
  await store.toggleAlbum(props.spotifyAlbumId)
}
</script>

<template>
  <Button
    :variant="variant"
    :size="size"
    :disabled="busy"
    :aria-label="`${label}${albumName ? `: ${albumName}` : ''}`"
    :title="connected ? label : 'Sign in with Spotify to play on your own device'"
    @click.stop.prevent="toggle"
  >
    <Loading03Icon v-if="busy" class="animate-spin" data-icon="inline-start" />
    <PauseIcon v-else-if="playing" data-icon="inline-start" />
    <SpotifyIcon v-else-if="!connected" class="text-[#1DB954]!" data-icon="inline-start" />
    <PlayIcon v-else data-icon="inline-start" />
    <span v-if="size !== 'icon'">{{ label }}</span>
  </Button>
</template>
