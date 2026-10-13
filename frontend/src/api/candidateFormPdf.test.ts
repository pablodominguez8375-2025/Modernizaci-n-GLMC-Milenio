import { afterEach, describe, expect, it, vi } from 'vitest'
import { CandidateIntakeApiClient } from './candidateIntakeApi'
import { PRIVATE_PDF_MEDIA, renderDemoCandidateCapturePdf } from './candidateFormPdf'
import { mockCandidateFormSchema, type CandidateFormCaptureDetail } from './candidateFormTypes'

const requestId = 'eeeeeeee-2222-2222-2222-222222222222'
const historical = (motivation: string): CandidateFormCaptureDetail => ({
  ceremonyRequestId: requestId, captureRevision: 4, schemaVersion: 1, scopeKey: 'order',
  capturedAtUtc: '2026-10-12T13:22:00Z',
  fields: mockCandidateFormSchema.fields.filter(field => !field.isCore),
  values: { custom_motivation: motivation, custom_volunteer: false },
})
const decoder = new TextDecoder('latin1')
afterEach(() => vi.unstubAllGlobals())

describe('Issue #37 — PDF A4 histórico privado', () => {
  it('creates a complete paginated PDF with original labels and no canonical identity', async () => {
    const blob = renderDemoCandidateCapturePdf(historical('Respuesta sintética á é í ó ú ñ'))
    expect(blob.type).toBe(PRIVATE_PDF_MEDIA)
    const bytes = new Uint8Array(await blob.arrayBuffer())
    const pdf = decoder.decode(bytes)
    expect(pdf.startsWith('%PDF-1.4')).toBe(true)
    expect(pdf).toContain('/MediaBox [0 0 595.28 841.89]')
    expect(pdf).toContain('/WinAnsiEncoding')
    expect(pdf).toContain('startxref\n')
    expect(pdf.endsWith('%%EOF\n')).toBe(true)
    expect(pdf).not.toContain('/JavaScript')
    expect(pdf).not.toContain('/EmbeddedFile')
    const lines = [...pdf.matchAll(/<([0-9A-F]+)> Tj/g)]
      .map(match => decoder.decode(Uint8Array.from(match[1].match(/.{2}/g) ?? [],
        item => Number.parseInt(item, 16))))
    const printable = lines.join(' ')
    expect(printable).toContain('Motivación personal complementaria')
    expect(printable).toContain('Respuesta sintética á é í ó ú ñ')
    expect(printable).toContain('Revisión: 4')
    expect(printable).not.toContain('rut_or_institutional_id')
    expect(printable).not.toContain('first_names')
    expect(printable).not.toContain('DEMO-16.543.219-X')

    const marker = pdf.lastIndexOf('startxref\n')
    const offset = Number.parseInt(pdf.slice(marker + 10).split('\n')[0], 10)
    expect(pdf.slice(offset, offset + 5)).toBe('xref\n')
  })

  it('preserves unsupported Unicode as explicit codepoints, without interpolating PDF actions', async () => {
    const blob = renderDemoCandidateCapturePdf(historical('Emoji 😀 símbolo <pdf> &'))
    const pdf = decoder.decode(await blob.arrayBuffer())
    const lines = [...pdf.matchAll(/<([0-9A-F]+)> Tj/g)]
      .map(match => decoder.decode(Uint8Array.from(match[1].match(/.{2}/g) ?? [],
        item => Number.parseInt(item, 16)))).join(' ')
    expect(lines).toContain('[U+1F600]')
    expect(lines).toContain('<pdf> &')
    expect(pdf).not.toContain('/URI ')
  })

  it('splits a long synthetic observation into more than one valid PDF page', async () => {
    const blob = renderDemoCandidateCapturePdf(historical(
      Array(180).fill('Antecedente de prueba no identificatorio para paginación.').join(' ')))
    const pdf = decoder.decode(await blob.arrayBuffer())
    const pages = pdf.match(/\/Type \/Page \/Parent/g) ?? []
    expect(pages.length).toBeGreaterThan(1)
    expect(pages.length).toBeLessThanOrEqual(120)
    expect(pdf).toContain('/Count ' + pages.length)
  })

  it('rejects extra payload keys and exposes only in-memory fake information', async () => {
    expect(() => renderDemoCandidateCapturePdf({
      ...historical('Texto ficticio'), values: { first_names: 'Prohibido' },
    })).toThrow('sin definición histórica')
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const blob = await api.exportCandidateFormCapturePdf(requestId, 1)
    expect(blob.type).toBe(PRIVATE_PDF_MEDIA)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('downloads only via authenticated backend and rejects HTML or forged PDF content', async () => {
    const valid = renderDemoCandidateCapturePdf(historical('Respuesta sintética'))
    const good = vi.fn(async () => new Response(await valid.arrayBuffer(),
      { status: 200, headers: { 'Content-Type': PRIVATE_PDF_MEDIA } }))
    vi.stubGlobal('fetch', good)
    const api = new CandidateIntakeApiClient({
      baseUrl: '/proxy', useMocks: false, getAccessToken: async () => 'ephemeral-secret',
    })
    await expect(api.exportCandidateFormCapturePdf(requestId, 2)).resolves.toBeInstanceOf(Blob)
    const [url, init] = good.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toContain('/formularios/capturas/2/export.pdf')
    expect(init.cache).toBe('no-store')
    expect(init.credentials).toBe('omit')
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer ephemeral-secret')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('invalid file',
      { status: 200, headers: { 'Content-Type': PRIVATE_PDF_MEDIA } })))
    await expect(api.exportCandidateFormCapturePdf(requestId, 1)).rejects.toThrow('cabecera válida')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('<html>invalid</html>',
      { status: 200, headers: { 'Content-Type': 'text/html' } })))
    await expect(api.exportCandidateFormCapturePdf(requestId, 1)).rejects.toThrow('PDF institucional')
  })
})
