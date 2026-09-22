import type { ReleaseDatePrecision } from '@/api'

/**
 * Spotify release dates arrive as `YYYY`, `YYYY-MM` or `YYYY-MM-DD` together
 * with a precision field. These helpers render and sort them correctly so a
 * `year`-precision album is not treated as a fresh release just because it
 * parsed late in the year.
 */

const MONTHS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
]

const MONTHS_SHORT = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
]

export function formatReleaseDate(
  releaseDate: string,
  precision: ReleaseDatePrecision,
): string {
  const [year, month, day] = releaseDate.split('-').map(Number)

  switch (precision) {
    case 'year': {
      if (!year) return releaseDate
      return String(year)
    }
    case 'month': {
      if (!year || !month) return releaseDate
      return `${MONTHS_SHORT[month - 1] ?? month} ${year}`
    }
    case 'day':
    default: {
      if (!year || !month || !day) return releaseDate
      const date = new Date(Date.UTC(year, month - 1, day))
      if (Number.isNaN(date.getTime())) return releaseDate
      return `${day} ${MONTHS[month - 1] ?? month} ${year}`
    }
  }
}

/** ISO sort key where lower precision normalizes to the earliest day. */
export function releaseSortKey(
  releaseDate: string,
  precision: ReleaseDatePrecision,
): string {
  const [year = '0000', month = '01', day = '01'] = releaseDate.split('-')
  if (precision === 'year') return `${year}-01-01`
  if (precision === 'month') return `${year}-${month || '01'}-01`
  return `${year}-${month || '01'}-${day || '01'}`
}

export function formatReleaseCountLabel(count: number): string {
  return `${count} ${count === 1 ? 'release' : 'releases'}`
}