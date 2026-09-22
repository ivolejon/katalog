<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { toast } from 'vue-sonner'
import type { LabelSummary } from '@/api'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  Avatar,
  AvatarFallback,
} from '@/components/ui/avatar'
import { useLabelsStore } from '@/stores/labels'
import { ArrowRight01Icon, Cancel01Icon, UserGroupIcon } from '@/lib/icons'

const props = defineProps<{
  label: LabelSummary
}>()

const store = useLabelsStore()
const removing = ref(false)

function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}

async function unfollow() {
  if (removing.value) {
    return
  }
  removing.value = true
  try {
    await store.removeLabel(props.label.id)
    toast.success('Label unfollowed', {
      description: `${props.label.name} was removed from your labels.`,
    })
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
  <Card class="gap-0! px-0! py-0!">
    <div class="flex items-center gap-4 p-4 sm:px-5">
      <RouterLink
        :to="{ name: 'label-detail', params: { id: label.id } }"
        class="flex min-w-0 flex-1 items-center gap-3"
      >
        <Avatar size="lg">
          <AvatarFallback class="bg-secondary text-secondary-foreground font-semibold">
            {{ initials(label.name) }}
          </AvatarFallback>
        </Avatar>
        <div class="flex min-w-0 flex-col">
          <span class="text-foreground truncate font-semibold">
            {{ label.name }}
          </span>
          <span class="text-muted-foreground flex items-center gap-1.5 text-sm">
            <UserGroupIcon class="size-3.5" />
            {{ label.artistCount }}
            {{ label.artistCount === 1 ? 'artist' : 'artists' }}
          </span>
        </div>
      </RouterLink>

      <div class="flex shrink-0 items-center gap-2">
        <Badge variant="secondary" class="hidden sm:inline-flex">
          Following
        </Badge>
        <Button
          variant="ghost"
          size="sm"
          class="text-muted-foreground"
          :disabled="removing"
          @click="unfollow"
        >
          <Cancel01Icon data-icon="inline-start" />
          Unfollow
        </Button>
        <RouterLink
          :to="{ name: 'label-detail', params: { id: label.id } }"
        >
          <Button variant="outline" size="icon-sm" aria-label="Open label">
            <ArrowRight01Icon />
          </Button>
        </RouterLink>
      </div>
    </div>
  </Card>
</template>