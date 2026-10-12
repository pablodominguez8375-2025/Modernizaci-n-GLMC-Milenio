import { afterEach, describe, expect, it, vi } from 'vitest'
import { CandidateIntakeApiClient } from './candidateIntakeApi'
import { PmgmApiClient } from './pmgmApi'
import { coreCandidateFormFields, type FormFieldDefinition } from './candidateFormTypes'

const demoRequestId = 'eeeeeeee-2222-2222-2222-222222222222'

afterEach(() => vi.unstubAllGlobals())

describe('Issue #37 — UI API and synthetic revision history', () => {
  it('uses the institutionally trusted schema and keeps old mock captures readable', async () => {
    const api = new CandidateIntakeApiClient({ useMocks: true })
    const active = await api.getCandidateFormSchema(demoRequestId)
    expect(active.formCode).toBe('candidate_profile')
    expect(active.schemaVersion).toBe(1)
    expect(active.expectedCaptureRevision).toBe(1)
    expect(active.fields.every(field => !field.isCore)).toBe(true)
    const before = await api.getCandidateFormCaptureHistory(demoRequestId)
    expect(before.total).toBe(1)
    expect(JSON.stringify(before)).not.toContain('contribuir a proyectos solidarios')
    const first = await api.getCandidateFormCapture(demoRequestId, 1)
    expect(first.schemaVersion).toBe(1)
    expect(first.fields?.find(field => field.key === 'custom_motivation')?.label)
      .toBe('Motivación personal complementaria')

    const next = await api.saveCandidateFormCapture(demoRequestId, {
      schemaVersion: active.schemaVersion, expectedCaptureRevision: active.expectedCaptureRevision,
      values: { custom_motivation: 'Observación exclusivamente ficticia', custom_volunteer: false },
    })
    expect(next.captureRevision).toBe(2)
    await expect(api.saveCandidateFormCapture(demoRequestId, {
      schemaVersion: 1, expectedCaptureRevision: 1,
      values: { custom_motivation: 'Intento obsoleto' },
    })).rejects.toThrow('Recargue')
    expect((await api.getCandidateFormCapture(demoRequestId, 1)).values)
      .toEqual(first.values)
    expect((await api.getCandidateFormCaptureHistory(demoRequestId)).total).toBe(2)
  })

  it('shows institutional catalogue, rejects invalid edits and schedules a new version in mock', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const catalogue = await api.getFormSchemaCatalog()
    expect(catalogue.forms[0].trustedCoreKeys).toEqual(coreCandidateFormFields.map(x => x.key))
    const old = await api.getFormSchemaVersions()
    const last = await api.getFormSchemaVersion(old.versions[0].version)
    const nextField: FormFieldDefinition = {
      key: 'custom_new_2026', label: 'Campo nuevo sintético', typeCode: 'text',
      isCore: false, enabled: true, visible: true, required: false, editable: true,
      classification: 'internal', sortOrder: 40,
    }
    await expect(api.publishFormSchemaVersion({
      version: old.versions[0].version + 1, expectedPreviousVersion: 0,
      effectiveFromUtc: '2099-04-01T10:00:00Z', fields: [...last.fields, nextField],
    })).rejects.toThrow('Recargue')
    const next = await api.publishFormSchemaVersion({
      version: old.versions[0].version + 1,
      expectedPreviousVersion: old.versions[0].version,
      effectiveFromUtc: '2099-04-01T10:00:00Z',
      fields: [...last.fields, nextField],
    })
    expect(next.version).toBe(old.versions[0].version + 1)
    expect((await api.getFormSchemaVersions()).total).toBe(old.total + 1)
    expect((await api.getFormSchemaVersion(next.version)).fields.some(f => f.key === 'custom_new_2026')).toBe(true)
  })

  it('uses bearer authorization and no browser cache for the real capture path', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      formCode: 'candidate_profile', schemaVersion: 1, scopeKey: 'order',
      effectiveFromUtc: '2026-10-01T00:00:00Z', expectedCaptureRevision: 0, fields: [],
    }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetchMock)
    const api = new CandidateIntakeApiClient({
      baseUrl: '/proxy', useMocks: false, getAccessToken: async () => 'opaque-ci-credential',
    })
    const result = await api.getCandidateFormSchema(demoRequestId)
    expect(result.formCode).toBe('candidate_profile')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toContain('/proxy/api/insinuados/solicitudes/')
    expect(url).toContain('/formularios/capturas/schema')
    expect(init.cache).toBe('no-store')
    expect(init.credentials).toBe('omit')
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer opaque-ci-credential')
  })
})
