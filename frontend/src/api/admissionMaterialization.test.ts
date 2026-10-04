import { afterEach, describe, expect, it, vi } from 'vitest'
import { PmgmApiClient } from './pmgmApi'
afterEach(() => vi.unstubAllGlobals())
describe('materialización y relectura institucional', () => {
  it('transporta el recibo exacto del servidor', async () => {
    const receipt = { idempotent: false, membershipId: 'membership-server', admissionCaseId: 'case-server', memberId: 'member-server', effectiveDate: '2026-10-03' }
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(receipt)))
    vi.stubGlobal('fetch', fetch)
    const api = new PmgmApiClient({ getAccessToken: async () => 'synthetic-token' })
    expect(await api.materializeAdmissionCase('case-server', { effectiveDate: '2026-10-03', evidenceReference: 'ACTA' })).toEqual(receipt)
    expect(fetch.mock.calls[0][0]).toBe('/api/admisiones/expedientes/case-server/materializar')
  })
  it('la demo rechaza reintentos con fecha o referencia diferentes', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    const payload = { effectiveDate: '2026-10-03', evidenceReference: 'SYNTHETIC ACTA' }
    expect((await api.materializeAdmissionCase('demo-crv-review', payload)).idempotent).toBe(false)
    expect((await api.materializeAdmissionCase('demo-crv-review', payload)).idempotent).toBe(true)
    await expect(api.materializeAdmissionCase('demo-crv-review', { ...payload, effectiveDate: '2026-10-02' })).rejects.toThrow()
    await expect(api.materializeAdmissionCase('demo-crv-review', { ...payload, evidenceReference: 'OTHER' })).rejects.toThrow()
    const saved = await api.getAdmissionCase('demo-crv-review')
    expect(saved.status).toBe('resolved')
    expect(saved.decisions.filter(x => x.decisionType === 'membership_materialized')).toHaveLength(1)
    expect(saved.decisions.find(x => x.decisionType === 'membership_materialized')?.sourceReference).toBe(payload.evidenceReference)
  })
})

 describe('permisos de la tramitación sintética', () => {
  it('Secretaría del Taller no recibe decisiones de Gran Maestría ni RI', async () => {
    const api = new PmgmApiClient({ useMocks: true })
    expect((await api.getAdmissionProcedure('demo-crv-review', 'lodgeSecretary')).actions).toEqual({ canManageLodge: true, canReviewSignature: false, canReviewArticle23: false, canProvideGrandMasterDecision: false })
    expect((await api.getAdmissionProcedure('demo-crv-review', 'regimen')).actions.canReviewArticle23).toBe(true)
    expect((await api.getAdmissionProcedure('demo-crv-review', 'grandMaster')).actions.canProvideGrandMasterDecision).toBe(true)
    expect((await api.getAdmissionProcedure('demo-crv-review', 'brother')).actions.canManageLodge).toBe(false)
  })
  it('un perfil sintético no sustituye los permisos de la API institucional', async () => {
    const response = { caseId: 'case-server', status: 'observed', canProceedToCeremonyRequest: false, requirements: [], actions: { canManageLodge: true, canReviewSignature: false, canReviewArticle23: false, canProvideGrandMasterDecision: false } }
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)))
    vi.stubGlobal('fetch', fetch)
    const api = new PmgmApiClient({ getAccessToken: async () => 'synthetic-token' })
    expect(await api.getAdmissionProcedure('case-server', 'grandMaster')).toEqual(response)
    expect(fetch.mock.calls[0][0]).toBe('/api/admisiones/expedientes/case-server/habilitacion-procedimiento')
  })
})
