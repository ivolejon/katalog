<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { RouterLink } from 'vue-router'
import { toast } from 'vue-sonner'
import { api } from '@/api'
import type { LabelDetail, AlbumSummary, LabelReleasesPage } from '@/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import AlbumCard from '@/components/albums/AlbumCard.vue'
import ArtistRow from '@/components/artists/ArtistRow.vue'
import EmptyState from '@/components/common/EmptyState.vue'
import ErrorState from '@/components/common/ErrorState.vue'
import { useLabelsStore } from '@/stores/labels'
import { releaseSortKey } from '@/utils/dates'
import { countUniqueArtists, mergeReleasePages } from '@/utils/paging'
import {
  ArrowLeft01Icon,
  Cancel01Icon,
  Disc01Icon,
  MusicNote01Icon,
  UserGroupIcon,
} from '@/lib/icons'

const props = defineProps<{
  id: string
}>()

const router = useRouter()
const store = useLabelsStore()

const PAGE_SIZE = 5

const detail = ref<LabelDetail | null>(null)
const loading = ref(true)
const notFound = ref(false)
const error = ref<string | null>(null)
const removing = ref(false)

const loadedReleases = ref<AlbumSummary[]>([])
const currentPage = ref(1)
const hasMore = ref(false)
const totalCount = ref(0)
const initialTotalCount = ref(0)
const loadingMore = ref(false)
const newReleasesCount = ref(0)

let loadRequest = 0
let loadMoreRequest = 0

const sortedReleases = computed(() => {
  return [...loadedReleases.value].sort((a, b) =>
    releaseSortKey(b.releaseDate, b.releaseDatePrecision).localeCompare(
      releaseSortKey(a.releaseDate, a.releaseDatePrecision),
    ),
  )
})

const displayedReleaseCount = computed(() => loadedReleases.value.length)
const displayedArtistCount = computed(() => countUniqueArtists(loadedReleases.value))

const newReleasesAvailable = computed(
  () => newReleasesCount.value > 0 && loadedReleases.value.length < totalCount.value,
)

const isFollowing = computed(() => {
  if (!detail.value) {
    return false
  }
  return store.labels.some((label) => label.id === detail.value?.id)
})

function resetPaging() {
  loadedReleases.value = []
  currentPage.value = 1
  hasMore.value = false
  totalCount.value = 0
  initialTotalCount.value = 0
  newReleasesCount.value = 0
}

function applyPage(page: LabelReleasesPage, isFirstPage: boolean) {
  if (isFirstPage) {
    loadedReleases.value = page.releases
    initialTotalCount.value = page.totalCount
    newReleasesCount.value = 0
  } else {
    loadedReleases.value = mergeReleasePages(loadedReleases.value, page)
    if (page.totalCount > initialTotalCount.value) {
      newReleasesCount.value = page.totalCount - initialTotalCount.value
    }
  }
  currentPage.value = page.page
  totalCount.value = page.totalCount
  hasMore.value = page.hasMore
}

async function load() {
  const request = ++loadRequest
  ++loadMoreRequest
  const id = props.id
  loading.value = true
  loadingMore.value = false
  notFound.value = false
  error.value = null
  detail.value = null
  resetPaging()

  try {
    const [labelDetail, firstPage] = await Promise.all([
      api.getLabel(id, false),
      api.getLabelReleases(id, 1, PAGE_SIZE),
    ])

    if (request !== loadRequest) {
      return
    }

    detail.value = labelDetail
    applyPage(firstPage, true)
  } catch (err) {
    if (request !== loadRequest) {
      return
    }
    if (err instanceof Error && 'status' in err && (err as { status: number }).status === 404) {
      notFound.value = true
    } else {
      error.value = err instanceof Error ? err.message : 'Failed to load label'
    }
  } finally {
    if (request === loadRequest) {
      loading.value = false
    }
  }
}

async function loadMore() {
  if (loadingMore.value || !hasMore.value) {
    return
  }

  const request = ++loadMoreRequest
  loadingMore.value = true
  const nextPage = currentPage.value + 1

  try {
    const page = await api.getLabelReleases(props.id, nextPage, PAGE_SIZE)
    if (request !== loadMoreRequest) {
      return
    }
    applyPage(page, false)
  } catch (err) {
    if (request !== loadMoreRequest) {
      return
    }
    toast.error('Could not load more releases', {
      description: err instanceof Error ? err.message : undefined,
    })
  } finally {
    if (request === loadMoreRequest) {
      loadingMore.value = false
    }
  }
}

async function reloadForNewReleases() {
  resetPaging()
  await load()
}

watch(() => props.id, load, { immediate: true })

async function unfollow() {
  if (!detail.value || removing.value) {
    return
  }
  removing.value = true
  const name = detail.value.name
  try {
    await store.removeLabel(detail.value.id)
    toast.success('Label unfollowed', {
      description: `${name} was removed from your labels.`,
    })
    await router.push({ name: 'labels' })
  } catch (err) {
    toast.error('Could not unfollow label', {
      description: err instanceof Error ? err.message : undefined,
    })
  } finally {
    removing.value = false
  }
}
</script>

<template>
  <div class="mx-auto w-full max-w-5xl px-4 py-8 sm:px-6 sm:py-12">
    <!-- Loading -->
    <div v-if="loading" class="flex flex-col gap-6">
      <Skeleton class="h-4 w-24" />
      <div class="flex flex-col gap-3">
        <Skeleton class="h-9 w-72 max-w-full" />
        <Skeleton class="h-4 w-48" />
      </div>
      <Skeleton class="h-10 w-72" />
      <div class="flex flex-col gap-3">
        <div
          v-for="i in 4"
          :key="i"
          class="flex items-center gap-3 rounded-2xl p-3"
        >
          <Skeleton class="size-10 rounded-full!" />
          <div class="flex flex-col gap-2">
            <Skeleton class="h-3.5 w-40" />
            <Skeleton class="h-3 w-28" />
          </div>
        </div>
      </div>
    </div>

    <!-- Not found -->
    <EmptyState
      v-else-if="notFound"
      :icon="Disc01Icon"
      title="Label not found"
      description="This label does not exist or has been removed."
    >
      <RouterLink to="/labels">
        <Button variant="outline">
          <ArrowLeft01Icon data-icon="inline-start" />
          Back to labels
        </Button>
      </RouterLink>
    </EmptyState>

    <!-- Error -->
    <ErrorState v-else-if="error" :description="error" @retry="load" />

    <template v-else-if="detail">
      <!-- Header -->
      <div class="mb-8 flex flex-col gap-6">
        <RouterLink
          to="/labels"
          class="text-muted-foreground hover:text-foreground flex w-fit items-center gap-1.5 text-sm font-medium transition-colors"
        >
          <ArrowLeft01Icon class="size-4" />
          All labels
        </RouterLink>
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div class="flex min-w-0 flex-col gap-2">
            <h1 class="text-foreground text-3xl font-bold tracking-tight sm:text-4xl">
              {{ detail.name }}
            </h1>
            <div class="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <span class="flex items-center gap-1.5">
                <UserGroupIcon class="size-4" />
                {{ displayedArtistCount }}
                {{ displayedArtistCount === 1 ? 'artist' : 'artists' }}
              </span>
              <span class="flex items-center gap-1.5">
                <Disc01Icon class="size-4" />
                {{ displayedReleaseCount }}
                {{ displayedReleaseCount === 1 ? 'release' : 'releases' }}
              </span>
              <Badge
                v-if="newReleasesAvailable"
                variant="outline"
                class="cursor-pointer"
                @click="reloadForNewReleases"
              >
                {{ newReleasesCount }} new since you started
              </Badge>
            </div>
          </div>
          <div class="flex shrink-0 items-center gap-2">
            <Badge v-if="isFollowing" variant="secondary">Following</Badge>
            <Button
              variant="outline"
              class="text-destructive!"
              :disabled="removing"
              @click="unfollow"
            >
              <Cancel01Icon data-icon="inline-start" />
              Unfollow
            </Button>
          </div>
        </div>
      </div>

      <Tabs default-value="releases">
        <TabsList class="w-fit!">
          <TabsTrigger value="releases">Releases</TabsTrigger>
          <TabsTrigger value="artists">Label artists</TabsTrigger>
        </TabsList>

        <!-- Releases -->
        <TabsContent value="releases" class="mt-4">
          <div v-if="sortedReleases.length" class="flex flex-col gap-3">
            <AlbumCard v-for="album in sortedReleases" :key="album.id" :album="album" />
            <Button
              v-if="hasMore"
              variant="outline"
              class="self-center"
              :disabled="loadingMore"
              @click="loadMore"
            >
              <template v-if="loadingMore">Laddar…</template>
              <template v-else>Ladda mer</template>
            </Button>
          </div>
          <EmptyState
            v-else
            :icon="MusicNote01Icon"
            title="No releases found"
            description="We couldn't find any albums or singles from this label's artists on Spotify."
          />
        </TabsContent>

        <!-- Artists -->
        <TabsContent value="artists" class="mt-2">
          <div v-if="detail.artists.length" class="rounded-2xl">
            <div
              v-for="(artist, index) in detail.artists"
              :key="artist.id"
              class="px-1"
            >
              <ArtistRow :artist="artist" />
              <Separator
                v-if="index < detail.artists.length - 1"
                class="bg-border/60"
              />
            </div>
          </div>
          <EmptyState
            v-else
            :icon="UserGroupIcon"
            title="No artists linked"
            description="Artists tied to this label will appear here as the backend discovers them."
          />
        </TabsContent>
      </Tabs>
    </template>
  </div>
</template>
