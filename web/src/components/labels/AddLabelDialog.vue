<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import type { ComponentPublicInstance } from 'vue'
import { toast } from 'vue-sonner'
import type { LabelSearchAlbum } from '@/api'
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
import { useLabelSearch } from '@/composables/useLabelSearch'
import { useLabelsStore } from '@/stores/labels'
import {
  Add01Icon,
  Cancel01Icon,
  Calendar01Icon,
  Disc01Icon,
  Loading03Icon,
  MusicNote02Icon,
  SearchIcon,
} from '@/lib/icons'

const emit = defineEmits<{
  added: []
}>()

const open = ref(false)
const store = useLabelsStore()

const {
  query: labelQuery,
  albums,
  matchedLabelName,
  searching: labelSearching,
  error: labelError,
  reset: resetLabelSearch,
} = useLabelSearch()
type Selection = { album: LabelSearchAlbum; labelName: string }
const selected = ref<Selection | null>(null)
const submitting = ref(false)
const error = ref<string | null>(null)
const inputEl = ref<ComponentPublicInstance | null>(null)

const selectionName = computed(() => {
  return selected.value?.labelName ?? ''
})

const selectionSpotifyIds = computed(() => {
  return selected.value ? [...new Set(selected.value.album.artists.map((a) => a.spotifyId))] : []
})

function pickAlbum(album: LabelSearchAlbum) {
  selected.value = { album, labelName: matchedLabelName.value || labelQuery.value.trim() }
  resetLabelSearch()
  error.value = null
}

function clearSelection() {
  selected.value = null
}

watch(open, async (isOpen) => {
  if (isOpen) {
    await nextTick()
    const el = inputEl.value?.$el
    if (el instanceof HTMLElement) {
      el.focus()
    }
  } else {
    selected.value = null
    resetLabelSearch()
    error.value = null
  }
})

async function submit() {
  if (!selected.value || submitting.value) {
    return
  }
  submitting.value = true
  error.value = null
  try {
    const label = await store.addLabel({
      name: selectionName.value,
      spotifyIds: selectionSpotifyIds.value,
    })
    toast.success('Label added', {
      description: `${label.name} is now being tracked.`,
    })
    emit('added')
    open.value = false
    selected.value = null
    resetLabelSearch()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not add label'
  } finally {
    submitting.value = false
  }
}</script>

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
          Search Spotify for a record label, then follow it.
        </DialogDescription>
      </DialogHeader>

      <div class="mt-4 flex flex-col gap-4">
            <div v-if="!selected" class="flex flex-col gap-2">
              <Label>Label name</Label>
              <div class="relative">
                <SearchIcon
                  class="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2"
                />
                <Input
                  ref="inputEl"
                  v-model="labelQuery"
                  placeholder="e.g. Globuli, Ninja Tune, Hyperdub"
                  class="pl-9"
                />
              </div>

              <ScrollArea class="h-64 rounded-xl">
                <div class="flex flex-col gap-1 pr-3">
                  <!-- Searching skeletons -->
                  <div v-if="labelSearching" class="flex flex-col gap-1">
                    <div
                      v-for="i in 4"
                      :key="i"
                      class="flex items-center gap-3 rounded-xl p-2"
                    >
                      <Skeleton class="size-12 rounded-lg!" />
                      <div class="flex flex-col gap-1.5">
                        <Skeleton class="h-3.5 w-44" />
                        <Skeleton class="h-3 w-28" />
                      </div>
                    </div>
                  </div>

                  <!-- Album hits -->
                  <template v-else-if="albums.length">
                    <button
                      v-for="album in albums"
                      :key="album.albumId"
                      type="button"
                      class="hover:bg-muted focus-visible:bg-muted flex w-full cursor-pointer items-center gap-3 rounded-xl p-2 text-left outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring"
                      @click="pickAlbum(album)"
                    >
                      <img
                        v-if="album.imageUrl"
                        :src="album.imageUrl"
                        :alt="album.name"
                        class="bg-muted size-12 shrink-0 rounded-lg object-cover"
                      />
                      <div
                        v-else
                        class="bg-muted text-muted-foreground flex size-12 shrink-0 items-center justify-center rounded-lg"
                      >
                        <Disc01Icon class="size-5" />
                      </div>
                      <div class="flex min-w-0 flex-col gap-0.5">
                        <span class="text-foreground truncate text-sm font-medium">
                          {{ album.name }}
                        </span>
                        <span class="text-muted-foreground truncate text-xs">
                          {{ album.artists.map((a) => a.name).join(', ') }}
                        </span>
                        <span
                          v-if="album.releaseDate"
                          class="text-muted-foreground/70 flex items-center gap-1 text-xs"
                        >
                          <Calendar01Icon class="size-3" />
                          {{ album.releaseDate }}
                        </span>
                      </div>
                    </button>

                  </template>

                  <!-- Quiet / empty / error states -->
                  <div
                    v-else-if="labelError"
                    class="text-muted-foreground flex flex-col items-center gap-2 p-8 text-center text-sm"
                  >
                    <Cancel01Icon class="size-5" />
                    <span>Search failed. Check that the backend is running.</span>
                    <span class="text-xs">{{ labelError }}</span>
                  </div>
                  <div
                    v-else-if="labelQuery"
                    class="text-muted-foreground flex flex-col items-center gap-2 p-8 text-center text-sm"
                  >
                    <MusicNote02Icon class="size-5" />
                    <span>No albums found for “{{ labelQuery }}”.</span>
                  </div>
                  <div
                    v-else
                    class="text-muted-foreground flex flex-col items-center gap-2 p-8 text-center text-sm"
                  >
                    <SearchIcon class="size-5" />
                    <span>Start typing to search Spotify for a label.</span>
                  </div>
                </div>
                <ScrollBar />
              </ScrollArea>
            </div>

            <!-- Selected label/album summary -->
            <div v-else class="flex flex-col gap-2">
              <Label>Label</Label>
              <div
                class="border-border/60 bg-secondary/50 flex items-center justify-between gap-3 rounded-2xl border p-3"
              >
                <div class="flex min-w-0 items-center gap-3">
                  <img
                    v-if="selected.album.imageUrl"
                    :src="selected.album.imageUrl"
                    :alt="selected.album.name"
                    class="bg-muted size-12 shrink-0 rounded-lg object-cover"
                  />
                  <div
                    v-else
                    class="bg-muted text-muted-foreground flex size-12 shrink-0 items-center justify-center rounded-lg"
                  >
                    <Disc01Icon class="size-5" />
                  </div>
                  <div class="flex min-w-0 flex-col">
                    <span class="text-foreground truncate text-sm font-medium">
                      {{ selectionName }}
                    </span>
                    <span class="text-muted-foreground truncate text-xs">
                      {{ selected.album.name }} · {{ selected.album.artists.map((a) => a.name).join(', ') }}
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
                Tracked as “{{ selectionName }}” with
                {{ selected.album.artists.length }}
                {{ selected.album.artists.length === 1 ? 'artist' : 'artists' }} from the
                album “{{ selected.album.name }}”.
              </p>
            </div>
      </div>

      <div v-if="error" class="bg-destructive/10 text-destructive rounded-xl px-3 py-2 text-sm">
        {{ error }}
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
