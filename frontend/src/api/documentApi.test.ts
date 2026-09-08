import { afterEach, expect, it, vi } from 'vitest'
import { DocumentApiClient } from './documentApi'

afterEach(() => vi.unstubAllGlobals())

it('uses bearer-only no-store boundary for the Virtual Library', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ total: 0, items: [] })))
  vi.stubGlobal('fetch', fetch)
  await new DocumentApiClient({ getAccessToken: async () => 'document-token' }).getLibrary()
  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/biblioteca')
  expect(options.headers.get('Authorization')).toBe('Bearer document-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('uses the dedicated collection document listing endpoint', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ total: 0, items: [] })))
  vi.stubGlobal('fetch', fetch)
  await new DocumentApiClient({ getAccessToken: async () => 'token' }).getDocuments('collection-1')
  expect(fetch.mock.calls[0][0]).toBe('/api/documentos/colecciones/collection-1/documentos')
})

it('library contract contains no storage key, hash, original filename or scan evidence', async () => {
  const payload = {
    total: 1,
    items: [{ id: 'd1', title: 'Documento', documentType: 'publication', collectionName: 'Biblioteca', versionNumber: 2, contentType: 'application/pdf', sizeBytes: 1000, publishedAtUtc: '2026-09-08T15:00:00Z' }],
  }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(payload)))
  vi.stubGlobal('fetch', fetch)
  const result = await new DocumentApiClient({ getAccessToken: async () => 'token' }).getLibrary()
  const serialized = JSON.stringify(result)
  expect(serialized).not.toContain('objectKey')
  expect(serialized).not.toContain('sha256')
  expect(serialized).not.toContain('originalFileName')
  expect(serialized).not.toContain('scanReference')
})

it('demo library works without token or network', async () => {
  const fetch = vi.fn(), token = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const result = await new DocumentApiClient({ useMocks: true, getAccessToken: token }).getLibrary()
  expect(result.total).toBeGreaterThan(0)
  expect(fetch).not.toHaveBeenCalled()
  expect(token).not.toHaveBeenCalled()
})
