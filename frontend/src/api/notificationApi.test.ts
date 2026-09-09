import { afterEach, expect, it, vi } from 'vitest'
import { NotificationApiClient } from './notificationApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only boundary for the personal inbox', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify([])))
  vi.stubGlobal('fetch', fetch)
  await new NotificationApiClient({ getAccessToken: async () => 'notification-token' }).getMine({ unreadOnly: true, limit: 5 })
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/notifications/me?unreadOnly=true&limit=5')
  expect(options.headers.get('Authorization')).toBe('Bearer notification-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('marks one notification as read using the dedicated endpoint', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetch)
  await new NotificationApiClient({ getAccessToken: async () => 'token' }).markRead('message-1')
  expect(fetch.mock.calls[0][0]).toBe('/api/notifications/message-1/read')
  expect(fetch.mock.calls[0][1].method).toBe('POST')
})

it('demo mode exposes a populated inbox and persists read state in-memory', async () => {
  const fetch = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new NotificationApiClient({ useMocks: true })
  const unreadBefore = await client.getMine({ unreadOnly: true })
  expect(unreadBefore.length).toBeGreaterThanOrEqual(3)
  expect(unreadBefore.some(item => item.mandatory)).toBe(true)
  await client.markRead(unreadBefore[0].id)
  const unreadAfter = await client.getMine({ unreadOnly: true })
  expect(unreadAfter.length).toBe(unreadBefore.length - 1)
  expect(fetch).not.toHaveBeenCalled()
})
