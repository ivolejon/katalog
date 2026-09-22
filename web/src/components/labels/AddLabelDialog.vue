<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import type { ComponentPublicInstance } from 'vue'
import { toast } from 'vue-sonner'
import type { ArtistSearchResult } from '@/api'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area'
import { Skeleton } from '@/components/ui/skeleton'
import { useArtistSearch } from '@/composables/useArtistSearch'
import { useLabelsStore } from '@/stores/labels'
import {
  Add01Icon,
  Cancel01Icon,
  Loading03Icon,
  MusicNote02Icon,
  SearchIcon,
} from '@/lib/icons'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'

const emit = defineEmits<{
  added: []
}>()

const open = ref(false)
const store = useLabelsStore()
const { query, results, searching, error: searchError, reset: resetSearch } = useArtistSearch()
const selected = ref<ArtistSearchResult | null>(null)
const submitting = ref(false)
const error = ref<string | null>(null)
const inputEl = ref<ComponentPublicInstance | null>(null)

const hasResults = computed(() => results.value.length > 0)

watch(open, async (isOpen) => {
  if (isOpen) {
    await nextTick()
    const el = inputEl.value?.$el
    if (el instanceof HTMLElement) {
      el.focus()
    }
  }
})

function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}

function pick(artist: ArtistSearchResult) {
  selected.value = artist
  resetSearch()
  error.value = null
}

function clearSelection() {
  selected.value = null
}

async function submit() {
  if (!selected.value || submitting.value) {
    return
  }
  submitting.value = true
  error.value = null
  try {
    const label = await store.addLabel({
      spotifyId: selected.value.id,
      name: selected.value.name,
    })
    toast.success('Label added', {
      description: `${label.name} is now being tracked.`,
    })
    emit('added')
    open.value = false
    selected.value = null
    resetSearch()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not add label'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <Button @click="open = true">
      Add label
      <Add01Icon data-icon="inline-end" />
    </Button>

    <DialogContent>
      <DialogHeader>
        <DialogTitle>Add a label</DialogTitle>
        <DialogDescription>
          Search Spotify for a label or artist profile, then follow it.
        </DialogDescription>
      </DialogHeader>

      <div class="flex flex-col gap-4">
        <!-- Artist picker -->
        <div v-if="!selected" class="flex flex-col gap-2">
          <Label>Search</Label>
          <div class="relative">
            <SearchIcon
              class="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
            />
            <Input
              ref="inputEl"
              v-model="query"
              placeholder="e.g. Hyperdub, Ninja Tune, XL Recordings"
              class="pl-9"
            />
          </div>

          <ScrollArea class="h-64 rounded-xl">
            <div class="flex flex-col gap-1 pr-3">
              <!-- Searching skeletons -->
              <div v-if="searching" class="flex flex-col gap-1">
                <div
                  v-for="i in 4"
                  :key="i"
                  class="flex items-center gap-3 rounded-xl p-2"
                >
                  <Skeleton class="size-8 rounded-full!" />
                  <div class="flex flex-col gap-1.5">
                    <Skeleton class="h-3.5 w-40" />
                    <Skeleton class="h-3 w-24" />
                  </div>
                </div>
              </div>

              <!-- Results -->
              <template v-else-if="hasResults">
                <button
                  v-for="artist in results"
                  :key="artist.id"
                  type="button"
                  class="hover:bg-muted focus-visible:bg-muted flex w-full cursor-pointer items-center gap-3 rounded-xl p-2 text-left outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring"
                  @click="pick(artist)"
                >
                  <Avatar>
                    <AvatarImage
                      v-if="artist.imageUrl"
                      :src="artist.imageUrl"
                      :alt="artist.name"
                    />
                    <AvatarFallback>
                      {{ initials(artist.name) }}
                    </AvatarFallback>
                  </Avatar>
                  <div class="flex min-w-0 flex-col">
                    <span class="text-foreground truncate text-sm font-medium">
                      {{ artist.name }}
                    </span>
                    <span
                      v-if="artist.genres.length"
                      class="text-muted-foreground truncate text-xs"
                    >
                      {{ artist.genres.slice(0, 3).join(', ') }}
                    </span>
                  </div>
                </button>
              </template>

              <!-- Quiet / empty / error states -->
              <div
                v-else-if="searchError"
                class="text-muted-foreground flex flex-col items-center gap-2 p-8 text-center text-sm"
              >
                <Cancel01Icon class="size-5" />
                <span>Search failed. Check that the backend is running.</span>
                <span class="text-xs">{{ searchError }}</span>
              </div>
              <div
                v-else-if="query"
                class="text-muted-foreground flex flex-col items-center gap-2 p-8 text-center text-sm"
              >
                <MusicNote02Icon class="size-5" />
                <span>No artists found for “{{ query }}”.</span>
              </div>
              <div
                v-else
                class="text-muted-foreground flex flex-col items-center gap-2 p-8 text-center text-sm"
              >
                <SearchIcon class="size-5" />
                <span>Start typing to search Spotify.</span>
              </div>
            </div>
            <ScrollBar />
          </ScrollArea>
        </div>

        <!-- Selected artist summary -->
        <div v-else class="flex flex-col gap-2">
          <Label>Label</Label>
          <div
            class="border-border/60 bg-secondary/50 flex items-center justify-between gap-3 rounded-2xl border p-3"
          >
            <div class="flex min-w-0 items-center gap-3">
              <Avatar size="lg">
                <AvatarImage
                  v-if="selected.imageUrl"
                  :src="selected.imageUrl"
                  :alt="selected.name"
                />
                <AvatarFallback>{{ initials(selected.name) }}</AvatarFallback>
              </Avatar>
              <div class="flex min-w-0 flex-col">
                <span class="text-foreground truncate text-sm font-medium">
                  {{ selected.name }}
                </span>
                <span
                  v-if="selected.genres.length"
                  class="text-muted-foreground truncate text-xs"
                >
                  {{ selected.genres.slice(0, 3).join(', ') }}
                </span>
              </div>
            </div>
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label="Clear selection"
              class="shrink-0"
              @click="clearSelection"
            >
              <Cancel01Icon />
            </Button>
          </div>
          <p class="text-muted-foreground text-xs">
            This will be tracked as “{{ selected.name }}” in your labels list.
          </p>
        </div>

        <div v-if="error" class="bg-destructive/10 text-destructive rounded-xl px-3 py-2 text-sm">
          {{ error }}
        </div>
      </div>

      <DialogFooter>
        <DialogClose as-child>
          <Button variant="ghost">Cancel</Button>
        </DialogClose>
        <Button
          :disabled="!selected || submitting"
          class="min-w-28"
          @click="submit"
        >
          <Loading03Icon
            v-if="submitting"
            class="animate-spin"
            data-icon="inline-start"
          />
          {{ submitting ? 'Adding…' : 'Follow label' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>