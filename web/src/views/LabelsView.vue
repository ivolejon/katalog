<script setup lang="ts">
import { onMounted } from 'vue'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import AddLabelDialog from '@/components/labels/AddLabelDialog.vue'
import LabelRow from '@/components/labels/LabelRow.vue'
import EmptyState from '@/components/common/EmptyState.vue'
import ErrorState from '@/components/common/ErrorState.vue'
import { useLabelsStore } from '@/stores/labels'
import { MusicNote01Icon } from '@/lib/icons'

const store = useLabelsStore()

onMounted(() => {
  void store.fetchLabels()
})

function retry() {
  void store.fetchLabels(true)
}
</script>

<template>
  <div class="mx-auto w-full max-w-5xl px-4 py-10 sm:px-6 sm:py-14">
    <div class="mb-8 flex flex-wrap items-end justify-between gap-4">
      <div class="flex flex-col gap-1.5">
        <h1 class="text-foreground text-3xl font-bold tracking-tight">
          Your labels
        </h1>
        <p class="text-muted-foreground text-sm">
          Labels you follow and their upcoming and recent releases.
        </p>
      </div>
      <div class="flex items-center gap-3">
        <Badge v-if="store.initialized && !store.error" variant="secondary">
          {{ store.labels.length }}
          {{ store.labels.length === 1 ? 'label' : 'labels' }}
        </Badge>
        <AddLabelDialog @added="store.fetchLabels(true)" />
      </div>
    </div>

    <!-- First load -->
    <div v-if="!store.initialized" class="flex flex-col gap-3">
      <div
        v-for="i in 3"
        :key="i"
        class="ring-foreground/10 flex items-center gap-4 rounded-2xl p-4 ring-1"
      >
        <Skeleton class="size-10 rounded-full!" />
        <div class="flex flex-col gap-2">
          <Skeleton class="h-4 w-44" />
          <Skeleton class="h-3.5 w-28" />
        </div>
      </div>
    </div>

    <!-- Error -->
    <ErrorState
      v-else-if="store.error"
      :description="store.error"
      @retry="retry"
    />

    <!-- Empty -->
    <EmptyState
      v-else-if="store.labels.length === 0"
      :icon="MusicNote01Icon"
      title="No labels yet"
      description="Add your first label with the button above to start following artists and releases."
    />

    <!-- List -->
    <div v-else class="flex flex-col gap-3">
      <LabelRow v-for="label in store.labels" :key="label.id" :label="label" />
    </div>
  </div>
</template>