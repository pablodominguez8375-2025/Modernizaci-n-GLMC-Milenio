import { afterEach, expect, it, vi } from 'vitest'
import { CalendarApiClient } from './calendarApi'

afterEach(() => vi.unstubAllGlobals())

it('queries calendar through the bearer-only institutional boundary', async () => {
  const payload = { fromUtc: '2026-09-01T00:00:00Z', toUtc: '2026-10-01T00:00:00Z', institutionalTimeZone: 'America/Santiago', events: [] }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(payload)))
  vi.stubGlobal('fetch', fetch)
  const client = new CalendarApiClient({ getAccessToken: async () => 'calendar-token' })

  await client.getCalendar({ fromUtc: payload.fromUtc, toUtc: payload.toUtc, organizationId: 'o1' })

  const [url, options] = fetch.mock.calls[0]
  expect(url).toContain('/api/calendar?')
  expect(url).toContain('organizationId=o1')
  expect(options.headers.get('Authorization')).toBe('Bearer calendar-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('demo mode exposes a showcase calendar without token or network', async () => {
  const fetch = vi.fn(), token = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new CalendarApiClient({ useMocks: true, getAccessToken: token })

  const result = await client.getCalendar({ fromUtc: '2026-09-01T00:00:00Z', toUtc: '2026-10-01T00:00:00Z' })

  expect(result.events.length).toBeGreaterThanOrEqual(5)
  expect(result.events.some(event => event.isMasked && event.title === 'Ocupado')).toBe(true)
  expect(result.events.some(event => event.eventType === 'lodge_meeting_day')).toBe(true)
  expect(result.events.some(event => event.eventType === 'ceremony_reservation')).toBe(true)
  expect(fetch).not.toHaveBeenCalled()
  expect(token).not.toHaveBeenCalled()
})

it('demo organization filtering does not leak events from another lodge', async () => {
  const client = new CalendarApiClient({ useMocks: true })
  const organizationId = '23232323-2323-2323-2323-232323232323'
  const result = await client.getCalendar({ fromUtc: '2026-09-01T00:00:00Z', toUtc: '2026-10-01T00:00:00Z', organizationId })

  expect(result.events.length).toBeGreaterThan(0)
  expect(result.events.every(event => event.organizationId === organizationId || event.organizationId === null)).toBe(true)
})

it('administrative reconciliation uses POST and bearer authentication', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ created: 1, updated: 2, skipped: 0 })))
  vi.stubGlobal('fetch', fetch)
  const client = new CalendarApiClient({ getAccessToken: async () => 'admin-token' })

  const result = await client.reconcileSources()

  expect(result).toEqual({ created: 1, updated: 2, skipped: 0 })
  expect(fetch.mock.calls[0][0]).toBe('/api/calendar/sources/reconcile')
  expect(fetch.mock.calls[0][1].method).toBe('POST')
  expect(fetch.mock.calls[0][1].headers.get('Authorization')).toBe('Bearer admin-token')
})
