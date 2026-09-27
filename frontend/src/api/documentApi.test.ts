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

it('sends Virtual Library search and pagination to the backend', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ total: 0, page: 2, pageSize: 12, items: [] })))
  vi.stubGlobal('fetch', fetch)

  await new DocumentApiClient({ getAccessToken: async () => 'library-token' }).searchLibrary({
    q: 'historia institucional',
    collectionId: 'collection-1',
    documentType: 'historical_publication',
    fromYear: 2020,
    toYear: 2026,
    page: 2,
    pageSize: 12,
  })

  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/biblioteca/buscar?q=historia+institucional&collectionId=collection-1&documentType=historical_publication&fromYear=2020&toYear=2026&page=2&pageSize=12')
  expect(options.headers.get('Authorization')).toBe('Bearer library-token')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
})

it('loads Library facets through the authenticated API boundary', async () => {
  const payload = { collections: [], documentTypes: [] }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(payload)))
  vi.stubGlobal('fetch', fetch)

  await new DocumentApiClient({ getAccessToken: async () => 'facet-token' }).getLibraryFacets()

  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/biblioteca/facetas')
  expect(options.headers.get('Authorization')).toBe('Bearer facet-token')
  expect(options.headers.get('Accept')).toBe('application/json')
})

it('downloads Library content through bearer auth without a public object-storage URL', async () => {
  const binary = new Uint8Array([37, 80, 68, 70, 45, 49, 46, 55])
  const fetch = vi.fn().mockResolvedValue(new Response(binary, { headers: { 'Content-Type': 'application/pdf' } }))
  vi.stubGlobal('fetch', fetch)

  const blob = await new DocumentApiClient({ getAccessToken: async () => 'download-token' }).downloadLibraryDocument('document-1')

  const [url, options] = fetch.mock.calls[0]
  expect(url).toBe('/api/biblioteca/document-1/contenido')
  expect(options.headers.get('Authorization')).toBe('Bearer download-token')
  expect(options.headers.get('Accept')).toBe('application/octet-stream')
  expect(options).toMatchObject({ credentials: 'omit', cache: 'no-store', redirect: 'error' })
  expect(blob.size).toBe(binary.byteLength)
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

it('demo library search and facets work without token or network', async () => {
  const fetch = vi.fn(), token = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const client = new DocumentApiClient({ useMocks: true, getAccessToken: token })

  const search = await client.searchLibrary({ q: 'historia', page: 1, pageSize: 12 })
  const facets = await client.getLibraryFacets()

  expect(search.total).toBeGreaterThan(0)
  expect(facets.collections.length).toBeGreaterThan(0)
  expect(facets.documentTypes.length).toBeGreaterThan(0)
  expect(fetch).not.toHaveBeenCalled()
  expect(token).not.toHaveBeenCalled()
})

it('demo library works without token or network', async () => {
  const fetch = vi.fn(), token = vi.fn()
  vi.stubGlobal('fetch', fetch)
  const result = await new DocumentApiClient({ useMocks: true, getAccessToken: token }).getLibrary()
  expect(result.total).toBeGreaterThan(0)
  expect(fetch).not.toHaveBeenCalled()
  expect(token).not.toHaveBeenCalled()
})

it('submits a work paper through the server scan pipeline and does not publish around the backend', async () => {
  const fetch = vi.fn()
    .mockResolvedValueOnce(new Response(JSON.stringify({ documentId: 'paper-1', versionId: 'version-1', versionNumber: 1, processingStatus: 'pending_upload' }), { status: 201 }))
    .mockResolvedValueOnce(new Response(JSON.stringify({ processingStatus: 'uploaded', sizeBytes: 8, hasIntegrityHash: true })))
    .mockResolvedValueOnce(new Response(JSON.stringify({ processingStatus: 'available', clean: true })))
  vi.stubGlobal('fetch', fetch)
  const client = new DocumentApiClient({ getAccessToken: async () => 'member-token' })
  const file = new File([new Uint8Array([37, 80, 68, 70, 45, 49, 46, 55])], 'plancha.pdf', { type: 'application/pdf' })

  await client.submitWorkPaper({ organizationId: 'workshop-23', title: 'Trabajo de prueba', shortDescription: 'Referencia breve', file })

  expect(fetch.mock.calls.map(call => call[0])).toEqual([
    '/api/biblioteca/mis-planchas',
    '/api/documentos/versiones/version-1/contenido',
    '/api/documentos/versiones/version-1/analizar',
  ])
  expect(JSON.parse(fetch.mock.calls[0][1].body)).toMatchObject({ organizationId: 'workshop-23', title: 'Trabajo de prueba', shortDescription: 'Referencia breve', authorMemberId: null })
  expect(fetch.mock.calls[0][1].headers.get('Authorization')).toBe('Bearer member-token')
})

it('demo work paper replacement retains older versions in the member history', async () => {
  const client = new DocumentApiClient({ useMocks: true })
  const file = new File(['revision'], 'revision.pdf', { type: 'application/pdf' })
  const before = await client.getMyWorkPapers()
  const id = before[0].id

  await client.replaceWorkPaper(id, { title: 'Trabajo revisado', shortDescription: 'Versión mejorada', file })

  const after = await client.getMyWorkPapers()
  expect(after[0].versions).toHaveLength(2)
  expect(after[0].versions[0]).toMatchObject({ versionNumber: 2, isCurrent: true, processingStatus: 'available' })
  expect(after[0].versions[1]).toMatchObject({ versionNumber: 1, isCurrent: false })
  expect(after[0].title).toBe('Trabajo revisado')
})

it('resolves a Tenida plancha link only through the authenticated work-paper endpoint', async () => {
  const payload = { documentId: 'paper-1', authorMemberId: 'member-1', title: 'Trabajo', shortDescription: 'Referencia' }
  const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(payload)))
  vi.stubGlobal('fetch', fetch)
  const result = await new DocumentApiClient({ getAccessToken: async () => 'secretariat-token' }).getWorkPaperByVersion('version-1')

  expect(result).toEqual(payload)
  expect(fetch.mock.calls[0][0]).toBe('/api/biblioteca/mis-planchas/versiones/version-1')
  expect(fetch.mock.calls[0][1].headers.get('Authorization')).toBe('Bearer secretariat-token')
})
