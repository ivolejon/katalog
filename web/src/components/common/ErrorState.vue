<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Alert01Icon } from '@/lib/icons'

defineProps<{
  title?: string
  description?: string
}>()

defineEmits<{
  retry: []
}>()
</script>

<template>
  <Card class="border-destructive/30!">
    <CardContent class="flex flex-col items-center gap-3 p-10! text-center">
      <span
        class="bg-destructive/10 text-destructive flex size-12 items-center justify-center rounded-2xl"
      >
        <Alert01Icon class="size-6" />
      </span>
      <div class="flex flex-col gap-1">
        <h3 class="text-foreground font-semibold">
          {{ title ?? 'Something went wrong' }}
        </h3>
        <p v-if="description" class="text-muted-foreground max-w-sm text-sm">
          {{ description }}
        </p>
      </div>
      <Button variant="outline" class="mt-1" size="sm" @click="$emit('retry')">
        Try again
      </Button>
    </CardContent>
  </Card>
</template>