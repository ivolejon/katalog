import type { IconArray } from '@hugeicons/vue'
import {
  Add01Icon as Add01IconData,
  Album01Icon as Album01IconData,
  Alert01Icon as Alert01IconData,
  Alert02Icon as Alert02IconData,
  ArrowLeft01Icon as ArrowLeft01IconData,
  ArrowRight01Icon as ArrowRight01IconData,
  ArrowUpRight02Icon as ArrowUpRight02IconData,
  Calendar01Icon as Calendar01IconData,
  Cancel01Icon as Cancel01IconData,
  CancelCircleIcon as CancelCircleIconData,
  CheckmarkCircle01Icon as CheckmarkCircle01IconData,
  Clock01Icon as Clock01IconData,
  DiscIcon as DiscIconData,
  InformationCircleIcon as InformationCircleIconData,
  Loading03Icon as Loading03IconData,
  MusicNote01Icon as MusicNote01IconData,
  MusicNote02Icon as MusicNote02IconData,
  Search01Icon as Search01IconData,
  SpotifyIcon as SpotifyIconData,
  Tick02Icon as Tick02IconData,
  UserGroupIcon as UserGroupIconData,
  Vynil01Icon as Vynil01IconData,
} from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/vue'
import { defineComponent, h } from 'vue'

/**
 * Icon shim for shadcn-vue components and app components.
 *
 * The shadcn-vue CLI generates components that import named icon components
 * from `@hugeicons/vue`, but that package only exports the generic
 * `HugeiconsIcon` component - the actual icon data lives in
 * `@hugeicons/core-free-icons`. This module bridges the two: it wraps the raw
 * icon shapes from `@hugeicons/core-free-icons` into Vue components.
 *
 * When re-adding shadcn-vue components, add any newly referenced icon here;
 * the names the generated components expect are kept intact.
 */
type IconSvgObject =
  | [string, Record<string, string | number>][]
  | readonly (readonly [string, Readonly<Record<string, string | number>>])[]

function iconComponent(icon: IconSvgObject) {
  return defineComponent({
    name: 'HugeiconsIconProxy',
    inheritAttrs: false,
    setup(_, { attrs }) {
      return () => h(HugeiconsIcon, { icon: icon as IconArray, ...attrs })
    },
  })
}

// Generated shadcn-vue component icons
export const SearchIcon = iconComponent(Search01IconData)
export const Tick02Icon = iconComponent(Tick02IconData)
export const Cancel01Icon = iconComponent(Cancel01IconData)
export const ArrowRight01Icon = iconComponent(ArrowRight01IconData)
export const CheckmarkCircle01Icon = iconComponent(CheckmarkCircle01IconData)
export const InformationCircleIcon = iconComponent(InformationCircleIconData)
export const Loading03Icon = iconComponent(Loading03IconData)
export const CancelCircleIcon = iconComponent(CancelCircleIconData)
export const Alert02Icon = iconComponent(Alert02IconData)

// App icons
export const Add01Icon = iconComponent(Add01IconData)
export const Album01Icon = iconComponent(Album01IconData)
export const Alert01Icon = iconComponent(Alert01IconData)
export const ArrowLeft01Icon = iconComponent(ArrowLeft01IconData)
export const ArrowUpRight02Icon = iconComponent(ArrowUpRight02IconData)
export const Calendar01Icon = iconComponent(Calendar01IconData)
export const Clock01Icon = iconComponent(Clock01IconData)
export const Disc01Icon = iconComponent(DiscIconData)
export const MusicNote01Icon = iconComponent(MusicNote01IconData)
export const MusicNote02Icon = iconComponent(MusicNote02IconData)
export const SpotifyIcon = iconComponent(SpotifyIconData)
export const UserGroupIcon = iconComponent(UserGroupIconData)
export const VinylIcon = iconComponent(Vynil01IconData)