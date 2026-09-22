<script setup lang="ts">
import type { LabelArtist } from '@/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
} from '@/components/ui/avatar'
import { SpotifyIcon } from '@/lib/icons'

defineProps<{
  artist: LabelArtist
}>()

function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}
</script>

<template>
  <div class="flex items-center gap-3 py-2.5">
    <Avatar size="lg">
      <AvatarImage
        v-if="artist.imageUrl"
        :src="artist.imageUrl"
        :alt="artist.name"
      />
      <AvatarFallback class="bg-secondary text-secondary-foreground font-semibold">
        {{ initials(artist.name) }}
      </AvatarFallback>
    </Avatar>

    <div class="flex min-w-0 flex-1 flex-col gap-0.5">
      <span class="text-foreground truncate text-sm font-medium">
        {{ artist.name }}
      </span>
      <span v-if="artist.genres?.length" class="text-muted-foreground truncate text-xs">
        {{ artist.genres.slice(0, 4).join(', ') }}
      </span>
    </div>

    <div class="flex shrink-0 items-center gap-2">
      <Badge
        v-if="artist.popularity !== null"
        variant="outline"
        class="hidden sm:inline-flex"
      >
        {{ artist.popularity }} popularity
      </Badge>
      <a
        v-if="artist.externalUrl"
        :href="artist.externalUrl"
        target="_blank"
        rel="noopener noreferrer"
        aria-label="Open artist on Spotify"
      >
        <Button variant="ghost" size="icon-sm">
          <SpotifyIcon class="text-[#1DB954]!" />
        </Button>
      </a>
    </div>
  </div>
</template>
