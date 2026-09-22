<script setup lang="ts">
import { computed } from 'vue'
import type { AlbumSummary } from '@/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { formatReleaseDate } from '@/utils/dates'
import { Album01Icon, Clock01Icon, SpotifyIcon } from '@/lib/icons'

const props = defineProps<{
  album: AlbumSummary
}>()

const albumTypeLabel = computed(() => {
  switch (props.album.albumType) {
    case 'single':
      return 'Single'
    case 'compilation':
      return 'Compilation'
    default:
      return 'Album'
  }
})
</script>

<template>
  <Card class="gap-0! px-0! py-0!">
    <CardContent class="flex items-start gap-4 p-4!">
      <div class="bg-muted/60 flex size-24 shrink-0 overflow-hidden rounded-2xl ring-1 ring-border/50 sm:size-28">
        <img
          v-if="album.imageUrl"
          :src="album.imageUrl"
          :alt="album.name"
          class="size-full object-cover"
          loading="lazy"
        />
        <div
          v-else
          class="text-muted-foreground flex size-full items-center justify-center"
        >
          <Album01Icon class="size-8" />
        </div>
      </div>

      <div class="flex min-w-0 flex-1 flex-col gap-2">
        <div class="flex items-start justify-between gap-3">
          <div class="flex min-w-0 flex-col gap-1">
            <h3 class="text-foreground truncate font-semibold" :title="album.name">
              {{ album.name }}
            </h3>
            <div class="flex flex-wrap items-center gap-2">
              <Badge variant="secondary">{{ albumTypeLabel }}</Badge>
              <span
                class="text-muted-foreground flex items-center gap-1 text-xs"
              >
                <Clock01Icon class="size-3.5" />
                {{ formatReleaseDate(album.releaseDate, album.releaseDatePrecision) }}
              </span>
            </div>
          </div>
          <a
            v-if="album.externalUrl"
            :href="album.externalUrl"
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button variant="outline" size="sm" class="shrink-0">
              <SpotifyIcon class="text-[#1DB954]!" data-icon="inline-start" />
              Open in Spotify
            </Button>
          </a>
        </div>
      </div>
    </CardContent>
  </Card>
</template>