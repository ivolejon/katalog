<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { Toaster } from '@/components/ui/sonner'
import { Disc01Icon } from '@/lib/icons'
</script>

<template>
  <div class="bg-background text-foreground flex min-h-svh flex-col">
    <header
      class="border-border/60 sticky top-0 z-40 border-b bg-background/80 backdrop-blur-md"
    >
      <div
        class="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-4 px-4 sm:px-6"
      >
        <RouterLink
          to="/"
          class="text-foreground flex items-center gap-2 text-base font-semibold tracking-tight transition-opacity hover:opacity-80"
        >
          <span
            class="bg-primary text-primary-foreground flex size-7 items-center justify-center rounded-xl"
          >
            <Disc01Icon class="size-4" />
          </span>
          Katalog
        </RouterLink>
        <nav class="flex items-center gap-1">
          <RouterLink
            to="/labels"
            class="hover:bg-muted text-muted-foreground hover:text-foreground rounded-full px-3 py-1.5 text-sm font-medium transition-colors"
            active-class="bg-muted text-foreground"
          >
            Labels
          </RouterLink>
        </nav>
      </div>
    </header>

    <main class="flex-1">
      <RouterView />
    </main>

    <Toaster position="bottom-right" rich-colors />
  </div>
</template>