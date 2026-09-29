<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useSpotifyStore } from '@/stores/spotify'
import { Alert01Icon, Loading03Icon, RefreshIcon, SpotifyIcon } from '@/lib/icons'

/**
 * Spotify Connect status: sign in, see which account is connected, and pick the device
 * releases are played on. The device list is fetched once here (and cached in the store);
 * it is not re-fetched per release card.
 */
const store = useSpotifyStore()

const devices = computed(() => store.availableDevices)
const targetDevice = computed(
  () => devices.value.find((device) => device.id === store.targetDeviceId) ?? null,
)

onMounted(() => {
  void store.loadSession()
})

function onDeviceChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  store.selectDevice(value || null)
}

function signOut() {
  void store.logout()
}
</script>

<template>
  <div class="ring-border/60 flex flex-col gap-3 rounded-2xl p-4 ring-1">
    <!-- Signed out -->
    <div v-if="!store.initialized" class="flex items-center gap-2">
      <Skeleton class="h-4 w-40" />
      <Skeleton class="h-9 w-28" />
    </div>

    <div v-else-if="!store.isConnected" class="flex flex-col gap-3">
      <div class="flex flex-col gap-1">
        <h2 class="text-foreground text-sm font-semibold">Play on your Spotify devices</h2>
        <p class="text-muted-foreground text-sm">
          Sign in with Spotify to play a release on your own speaker or phone through
          Spotify Connect.
        </p>
      </div>
      <div>
        <Button size="sm" class="w-fit" @click="store.login()">
          <SpotifyIcon class="text-[#1DB954]!" data-icon="inline-start" />
          Sign in with Spotify
        </Button>
      </div>
    </div>

    <!-- Signed in -->
    <div v-else class="flex flex-col gap-3">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <div class="flex min-w-0 flex-col gap-0.5">
          <h2 class="text-foreground truncate text-sm font-semibold">
            {{ store.session?.displayName ?? 'Spotify' }}
          </h2>
          <p class="text-muted-foreground truncate text-xs">
            <template v-if="store.hasPremium">Spotify Premium</template>
            <template v-else>Free account - playback control needs Premium</template>
          </p>
        </div>
        <Button variant="ghost" size="sm" class="shrink-0" @click="signOut">
          Sign out
        </Button>
      </div>

      <!-- No Premium: playing here is impossible, so say it before the user clicks. -->
      <p
        v-if="!store.hasPremium"
        class="text-muted-foreground flex items-start gap-2 text-xs"
      >
        <Alert01Icon class="mt-px size-3.5 shrink-0" />
        <span>
          Controlling playback from Katalog requires a Spotify Premium account.
        </span>
      </p>

      <div class="flex flex-wrap items-end gap-2">
        <div class="flex min-w-0 flex-col gap-1">
          <label for="spotify-device" class="text-muted-foreground text-xs font-medium">
            Play on
          </label>
          <div v-if="store.devicesLoading && devices.length === 0" class="flex items-center gap-2">
            <Skeleton class="h-9 w-44" />
          </div>
          <select
            v-else
            id="spotify-device"
            :value="store.targetDeviceId ?? ''"
            class="bg-input/30 border-input focus-visible:border-ring focus-visible:ring-ring/50 h-9 w-full min-w-44 rounded-4xl border px-3 text-sm outline-none focus-visible:ring-3"
            :disabled="devices.length === 0"
            @change="onDeviceChange"
          >
            <option v-if="devices.length === 0" value="">
              No devices available
            </option>
            <option
              v-for="device in devices"
              :key="device.id"
              :value="device.id"
            >
              {{ device.name }} ({{ device.type }}){{ device.isActive ? ' - active' : '' }}
            </option>
          </select>
        </div>
        <Button
          variant="outline"
          size="sm"
          :disabled="store.devicesLoading"
          title="Refresh devices"
          @click="store.loadDevices(true)"
        >
          <Loading03Icon v-if="store.devicesLoading" class="animate-spin" data-icon="inline-start" />
          <RefreshIcon v-else data-icon="inline-start" />
          Refresh
        </Button>
      </div>

      <p v-if="store.devicesError" class="text-destructive flex items-start gap-2 text-xs">
        <Alert01Icon class="mt-px size-3.5 shrink-0" />
        <span>{{ store.devicesError }}</span>
      </p>
      <p
        v-else-if="!store.devicesLoading && devices.length === 0"
        class="text-muted-foreground text-xs"
      >
        No Spotify devices found. Open the Spotify app on the device you want to hear, then
        refresh.
      </p>
      <p v-else-if="targetDevice" class="text-muted-foreground text-xs">
        Releases you play will start on {{ targetDevice.name }}.
      </p>
    </div>
  </div>
</template>
