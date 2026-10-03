/** Calendar dates, matching DateOnly.AddMonths(3) in the API (not 90 days). */
export function chileCivilDate(now = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'America/Santiago', year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(now)
  const value = (type: string) => parts.find(part => part.type === type)?.value
  return `${value('year')}-${value('month')}-${value('day')}`
}

export function affiliationModeForDate(grantedDate: string, today = chileCivilDate()): 'simple' | 'activation' | null {
  const parse = (value: string) => {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return null
    const [year, month, day] = value.split('-').map(Number)
    if (year < 1 || month < 1 || month > 12 || day < 1) return null
    const date = new Date(0)
    date.setUTCFullYear(year, month - 1, day)
    date.setUTCHours(0, 0, 0, 0)
    return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day ? date : null
  }
  const granted = parse(grantedDate)
  if (!granted || !parse(today) || grantedDate > today) return null
  const lastDay = new Date(granted)
  lastDay.setUTCFullYear(granted.getUTCFullYear(), granted.getUTCMonth() + 4, 0)
  const cutoff = new Date(granted)
  cutoff.setUTCFullYear(lastDay.getUTCFullYear(), lastDay.getUTCMonth(), Math.min(granted.getUTCDate(), lastDay.getUTCDate()))
  return today <= cutoff.toISOString().slice(0, 10) ? 'simple' : 'activation'
}
