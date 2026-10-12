import { afterEach, describe, expect, it, vi } from 'vitest'
import { CandidateIntakeApiClient } from './candidateIntakeApi'
import { renderDemoCandidateCaptureCsv } from './candidateFormCsv'
import { mockCandidateFormSchema, type CandidateFormCaptureDetail } from './candidateFormTypes'

const requestId = 'eeeeeeee-2222-2222-2222-222222222222'
const detail = (value: string): CandidateFormCaptureDetail => ({
  ceremonyRequestId: requestId,
  captureRevision: 3,
  schemaVersion: 1,
  scopeKey: 'order',
  capturedAtUtc: '2026-10-12T14:30:00Z',
  fields: mockCandidateFormSchema.fields.filter(field => !field.isCore),
  values: { custom_motivation: value, custom_volunteer: false }
})

afterEach(() => vi.unstubAllGlobals())

describe('Issue #37 — exportación de revisión histórica aislada', () => {
  it('uses original labels without copying canonical identity and retains false', () => {
    const csv = renderDemoCandidateCaptureCsv(detail('Motivación sintética privada'))
    expect(csv.startsWith('\uFEFF')).toBe(true)
    expect(csv).toContain('"Motivación personal complementaria"')
    expect(csv).toContain('"custom_motivation"')
    expect(csv).toContain('"No"')
    expect(csv).not.toContain('first_names')
    expect(csv).not.toContain('rut_or_institutional_id')
    expect(csv).not.toContain('Tomás Ignacio')
  })

  it.each([
    '=HYPERLINK("https://invalid.test","malicia")', '  +SUM(1,2)', '\t@IMPORTXML(1)',
    '\n-1+5',
  ])('neutralizes a potential spreadsheet formula: %s', value => {
    const csv = renderDemoCandidateCaptureCsv(detail(value))
    expect(csv).toContain('"\'' + value.replace(/"/g, '""') + '"')
    expect(detail(value).values.custom_motivation).toBe(value)
  })

  it('rejects unmapped or canonical private keys', () => {
    expect(() => renderDemoCandidateCaptureCsv({
      ...detail('Ficticio'), values: { custom_motivation: 'Ficticio', first_names: 'NO' },
    })).toThrow('sin definición')
  })

  it('uses mocked data only in memory and returns UTF-8 CSV Blob', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const blob = await api.exportCandidateFormCaptureCsv(requestId, 1)
    expect(blob.type).toContain('text/csv')
    expect(await blob.text()).toContain('Motivación personal complementaria')
    expect(await blob.text()).not.toContain('rut_or_institutional_id')
  })

  it('requests authenticated private export from the institutional API, never via a public URL', async () => {
    const fetchMock = vi.fn(async () => new Response('\uFEFFCampo;Clave;Valor\r\n', {
      status: 200, headers: { 'Content-Type': 'text/csv; charset=utf-8' },
    }))
    vi.stubGlobal('fetch', fetchMock)
    const api = new CandidateIntakeApiClient({
      baseUrl: '/proxy', useMocks: false, getAccessToken: async () => 'ephemeral-token',
    })
    const csv = await api.exportCandidateFormCaptureCsv(requestId, 2)
    expect(csv.type).toContain('text/csv')
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toContain('/api/insinuados/solicitudes/')
    expect(url).toContain('/formularios/capturas/2/export.csv')
    expect(init.cache).toBe('no-store')
    expect(init.credentials).toBe('omit')
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer ephemeral-token')
  })

  it('refuses a non-CSV API payload rather than downloading unexpected content', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('<html>Error</html>', {
      status: 200, headers: { 'Content-Type': 'text/html' },
    })))
    const api = new CandidateIntakeApiClient({
      baseUrl: '/proxy', useMocks: false, getAccessToken: async () => 'ephemeral-token',
    })
    await expect(api.exportCandidateFormCaptureCsv(requestId, 1)).rejects.toThrow('CSV autorizado')
  })
})
