<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { ArrowRight01Icon, Disc01Icon, MusicNote02Icon, VinylIcon } from '@/lib/icons'

const features = [
  {
    icon: VinylIcon,
    title: 'Follow labels',
    description:
      'Pick any record label from Spotify and follow it - no account needed.',
  },
  {
    icon: MusicNote02Icon,
    title: 'Artists per label',
    description:
      'See the artists signed to each label at a glance, with cover art and genre tags.',
  },
  {
    icon: Disc01Icon,
    title: 'New releases feed',
    description:
      'Albums and singles from your labels, sorted by release date, with a direct link into Spotify.',
  },
]

</script>

<template>
  <div class="mx-auto w-full max-w-5xl px-4 sm:px-6">
    <!-- Hero -->
    <section class="pt-20 pb-14 sm:pt-28 sm:pb-20">
      <div class="flex flex-col items-center gap-6 text-center">
        <span
          class="bg-secondary text-secondary-foreground rounded-full px-3 py-1 text-xs font-medium"
        >
          Spotify label tracker
        </span>
        <h1
          class="text-foreground max-w-2xl text-4xl font-bold tracking-tight text-balance sm:text-5xl"
        >
          Follow the labels that shape the music you love
        </h1>
        <p class="text-muted-foreground max-w-xl text-lg text-pretty">
          Katalog keeps an eye on record labels through the Spotify catalog -
          add a label, browse its artists, and catch every new release in one
          place.
        </p>
        <div class="flex items-center gap-3">
          <RouterLink to="/labels">
            <Button size="lg">
              Explore labels
              <ArrowRight01Icon data-icon="inline-end" />
            </Button>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Feature cards -->
    <section
      class="border-border/60 grid gap-4 border-t py-14 sm:grid-cols-3"
    >
      <Card v-for="feature in features" :key="feature.title">
        <CardContent class="gap-4! p-6!">
          <span
            class="bg-secondary text-secondary-foreground flex size-10 items-center justify-center rounded-2xl"
          >
            <component :is="feature.icon" class="size-5" />
          </span>
          <div class="flex flex-col gap-1">
            <h3 class="text-foreground font-semibold">{{ feature.title }}</h3>
            <p class="text-muted-foreground text-sm text-pretty">
              {{ feature.description }}
            </p>
          </div>
        </CardContent>
      </Card>
    </section>

    <!-- About strip -->
    <section class="pb-20">
      <Card class="bg-muted/40">
        <CardContent class="gap-2 p-8! text-center">
          <p class="text-muted-foreground text-sm">
            A single-user MVP: your labels, your feed. No sign-up, no tokens in
            the browser - the backend talks to Spotify for you.
          </p>
        </CardContent>
      </Card>
    </section>
  </div>
</template>