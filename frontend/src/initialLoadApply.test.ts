import { describe, expect, it } from 'vitest'
import { applyThenVerify, applyReadiness, blockerRows, checkReason, describeApplyHttpError, describeApplyResult, describeBlocker, describeReadBack } from './initialLoadApply'
import type { InitialLoadPreviewResult } from './initialLoadPreview'

const preview = (over: Partial<InitialLoadPreviewResult> = {}): InitialLoadPreviewResult => ({
  valid: true, canApply: false, writesPerformed: 0, status: 'PREVIEW_VALID', dataRowCounts: {}, findings: [],
  applicable: true, applyBlockers: [], receipt: 'r', receiptExpiresAtUtc: '2030-01-01T00:00:00Z', ...over })
const now = Date.parse('2026-11-20T12:00:00Z')

describe('applyReadiness', () => {
  it('needs a preview, an applicable batch with receipt, an unexpired receipt and an open window', () => {
    expect(applyReadiness(null, 'open', now).canApply).toBe(false)
    expect(applyReadiness(preview({ applicable: false }), 'open', now).canApply).toBe(false)
    expect(applyReadiness(preview({ receipt: null }), 'open', now).canApply).toBe(false)
    expect(applyReadiness(preview({ valid: false, applicable: false }), 'open', now).reason).toMatch(/Corrija/)
    expect(applyReadiness(preview({ receiptExpiresAtUtc: '2026-11-20T11:59:00Z' }), 'open', now).reason).toMatch(/venció/)
    expect(applyReadiness(preview(), 'not_opened', now).canApply).toBe(false)
    expect(applyReadiness(preview(), 'closed', now).canApply).toBe(false)
    expect(applyReadiness(preview(), undefined, now).canApply).toBe(false)
    expect(applyReadiness(preview(), 'open', now).canApply).toBe(true)
  })
})

describe('texts', () => {
  it('validates the audit reason length', () => {
    expect(checkReason('corto')).not.toBeNull()
    expect(checkReason('   ' + 'x'.repeat(9))).not.toBeNull()
    expect(checkReason('Apertura de la carga inicial')).toBeNull()
    expect(checkReason('x'.repeat(501))).not.toBeNull()
  })
  it('never claims success when not applied and reports replays without duplication', () => {
    const counts = { workshopsCreated: 1, membersCreated: 2, memberExactSkips: 0, officesCreated: 1, officeExactSkips: 0 }
    expect(describeApplyResult({ applied: true, alreadyApplied: false, code: 'OK', counts }).title).toBe('La carga se aplicó')
    expect(describeApplyResult({ applied: false, alreadyApplied: true, code: 'OK', counts }).lines[0]).toMatch(/No se duplicó/)
    const failed = describeApplyResult({ applied: false, alreadyApplied: false, code: 'MEMBER_CONFLICT_EXISTING_PERSON', counts })
    expect(failed.tone).toBe('warn')
    expect(failed.lines.join(' ')).toMatch(/No se guardó nada/)
  })
  it('maps http statuses and codes to plain Spanish', () => {
    expect(describeApplyHttpError(404)).toMatch(/no está habilitada/)
    expect(describeApplyHttpError(403)).toMatch(/Máster/)
    expect(describeApplyHttpError(409, 'WINDOW_NOT_OPEN')).toMatch(/Ábrala/)
    expect(describeApplyHttpError(422, 'RECEIPT_EXPIRED')).toMatch(/venció/)
    expect(describeApplyHttpError(500)).toMatch(/No se guardó nada/)
  })
  it('describes blockers without echoing data and read-back counts', () => {
    expect(describeBlocker('MEMBER_BATCH_LIMIT_30')).toMatch(/30/)
    expect(describeBlocker('UNKNOWN')).toMatch(/impide/)
    expect(blockerRows([{ sheet: 'Hermanos.csv', row: 4, field: 'rows', code: 'STATUS_NOT_APPLICABLE' }])[0].where).toBe('Hermanos.csv, fila 4')
    expect(describeReadBack({ windowStatus: 'open', workshops: 1, members: 2, memberships: 2, officeAssignments: 1, batches: [] })).toEqual(['Talleres cargados: 1', 'Hermanos: 2', 'Membresías: 2', 'Cargos: 1'])
  })
})

describe('applyThenVerify', () => {
  const counts = { workshopsCreated: 1, membersCreated: 1, memberExactSkips: 0, officesCreated: 1, officeExactSkips: 0 }
  const ok = { applied: true, alreadyApplied: false, code: 'OK', counts }
  it('keeps the apply result when the read-back fails (503) and never reports a rollback', async () => {
    const outcome = await applyThenVerify(async () => ok, async () => { throw new Error('503') })
    expect(outcome.result.applied).toBe(true)
    expect(outcome.readBack).toBeNull()
    expect(outcome.readBackFailed).toBe(true)
  })
  it('returns both when everything works and propagates a failed apply untouched', async () => {
    const rb = { windowStatus: 'open', workshops: 1, members: 1, memberships: 1, officeAssignments: 1, batches: [] }
    expect(await applyThenVerify(async () => ok, async () => rb)).toEqual({ result: ok, readBack: rb, readBackFailed: false })
    let read = 0
    await expect(applyThenVerify(async () => { throw new Error('422') }, async () => { read++; return rb })).rejects.toThrow('422')
    expect(read).toBe(0)
  })
})
