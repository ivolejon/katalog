<script setup lang="ts">
import { onMounted } from 'vue'
import AppShell from '@/components/layout/AppShell.vue'
import { useSpotifyStore } from '@/stores/spotify'

const spotify = useSpotifyStore()

onMounted(() => {
  // After the Spotify sign-in the backend sends the browser back here with ?spotify=connected
  // (or ?spotify=failed&reason=...); handle that once and clean the URL.
  void spotify.consumeLoginCallback()
  if (!spotify.initialized) {
    void spotify.loadSession()
  }
})
</script>

<template>
  <AppShell />
</template>
