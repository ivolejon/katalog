<script setup lang="ts">
import type { Component } from 'vue'
import { Card, CardContent } from '@/components/ui/card'

defineProps<{
  icon?: Component
  title: string
  description?: string
}>()
</script>

<template>
  <Card class="border-dashed! bg-transparent!">
    <CardContent
      class="flex flex-col items-center gap-3 p-10! text-center"
    >
      <span
        v-if="icon"
        class="bg-muted text-muted-foreground flex size-12 items-center justify-center rounded-2xl"
      >
        <component :is="icon" class="size-6" />
      </span>
      <div class="flex flex-col gap-1">
        <h3 class="text-foreground font-semibold">{{ title }}</h3>
        <p v-if="description" class="text-muted-foreground max-w-sm text-sm">
          {{ description }}
        </p>
      </div>
      <div v-if="$slots.default" class="mt-1">
        <slot />
      </div>
    </CardContent>
  </Card>
</template>