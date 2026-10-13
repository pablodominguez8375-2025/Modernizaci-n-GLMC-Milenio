import { afterEach, describe, expect, it, vi } from 'vitest'
import { CandidateIntakeApiClient } from './candidateIntakeApi'
import { PRIVATE_DOCX_MEDIA, renderDemoCandidateCaptureDocx } from './candidateFormDocx'
import { mockCandidateFormSchema, type CandidateFormCaptureDetail } from './candidateFormTypes'

const requestId = 'eeeeeeee-2222-2222-2222-222222222222'
const mockDetail = (motivation: string): CandidateFormCaptureDetail => ({
  ceremonyRequestId: requestId,
  captureRevision: 4,
  schemaVersion: 1,
  scopeKey: 'order',
  capturedAtUtc: '2026-10-12T13:22:00Z',
  fields: mockCandidateFormSchema.fields.filter(field => !field.isCore),
  values: { custom_motivation: motivation, custom_volunteer: false },
})

afterEach(() => vi.unstubAllGlobals())

describe('Issue #37 — Word histórico privado', () => {
  it('creates a real ZIP/OpenXML file with UTF-8 private fields and no canonical identity', async () => {
    const blob = renderDemoCandidateCaptureDocx(mockDetail('Texto ficticio con acentos: á ñ é.'))
    expect(blob.type).toBe(PRIVATE_DOCX_MEDIA)
    const bytes = new Uint8Array(await blob.arrayBuffer())
    expect(Array.from(bytes.slice(0, 4))).toEqual([0x50, 0x4b, 0x03, 0x04])
    const content = new TextDecoder().decode(bytes)
    expect(content).toContain('[Content_Types].xml')
    expect(content).toContain('word/document.xml')
    expect(content).toContain('Motivación personal complementaria')
    expect(content).toContain('Texto ficticio con acentos: á ñ é.')
    expect(content).toContain('Revisión: 4')
    expect(content).not.toContain('rut_or_institutional_id')
    expect(content).not.toContain('first_names')
    expect(content).not.toContain('DEMO-16.543.219-X')
  })

  it('escapes markup and rejects core keys even if the user provides them', async () => {
    const blob = renderDemoCandidateCaptureDocx(mockDetail('Valor <etiqueta> & texto'))
    const content = new TextDecoder().decode(await blob.arrayBuffer())
    expect(content).toContain('Valor &lt;etiqueta&gt; &amp; texto')
    expect(content).not.toContain('Valor <etiqueta>')
    expect(() => renderDemoCandidateCaptureDocx({
      ...mockDetail('Ejemplo'), values: { custom_motivation: 'Ejemplo', first_names: 'Prohibido' },
    })).toThrow('sin definición histórica')
  })

  it('returns fictional data from volatile mock client without network access', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const blob = await api.exportCandidateFormCaptureDocx(requestId, 1)
    expect(blob.type).toBe(PRIVATE_DOCX_MEDIA)
    expect(fetchMock).not.toHaveBeenCalled()
    const content = new TextDecoder().decode(await blob.arrayBuffer())
    expect(content).toContain('DEMO')
  })

  it('downloads from the authenticated backend, refusing fake MIME and corrupted ZIP', async () => {
    const payload = renderDemoCandidateCaptureDocx(mockDetail('Solo para prueba'))
    const goodFetch = vi.fn(async () => new Response(await payload.arrayBuffer(), {
      status: 200, headers: { 'Content-Type': PRIVATE_DOCX_MEDIA },
    }))
    vi.stubGlobal('fetch', goodFetch)
    const api = new CandidateIntakeApiClient({
      baseUrl: '/proxy', useMocks: false, getAccessToken: async () => 'ephemeral-credential',
    })
    const docx = await api.exportCandidateFormCaptureDocx(requestId, 2)
    expect(docx.type).toBe(PRIVATE_DOCX_MEDIA)
    const [url, init] = goodFetch.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toContain('/formularios/capturas/2/export.docx')
    expect(init.cache).toBe('no-store')
    expect(init.credentials).toBe('omit')
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer ephemeral-credential')

    vi.stubGlobal('fetch', vi.fn(async () => new Response('not a Word file', {
      status: 200, headers: { 'Content-Type': PRIVATE_DOCX_MEDIA },
    })))
    await expect(api.exportCandidateFormCaptureDocx(requestId, 1))
      .rejects.toThrow('paquete válido')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('Unexpected response', {
      status: 200, headers: { 'Content-Type': 'text/html' },
    })))
    await expect(api.exportCandidateFormCaptureDocx(requestId, 1))
      .rejects.toThrow('documento Word autorizado')
  })
})
