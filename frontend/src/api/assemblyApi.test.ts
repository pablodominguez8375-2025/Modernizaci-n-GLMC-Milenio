import { describe, expect, it } from 'vitest'
import { MockAssemblyStore, assemblyStatusLabel } from './assemblyApi'

const ID = 'demo-asamblea-2026'

describe('padrón de la Gran Asamblea (demo)', () => {
  it('orders both listings alphabetically by surname (Ñ after N) and masks every RUT', () => {
    const listing = new MockAssemblyStore().listing(ID)
    const names = listing.asambleistas.map(r => r.name)
    expect(names).toEqual([...names].sort((a, b) => a.localeCompare(b, 'es-CL')))
    expect(listing.asambleistas.every(r => /\*\*\*-[\dK]$/.test(r.rut))).toBe(true)
    expect(listing.asambleistas.every(r => r.signature === '')).toBe(true)
    expect(listing.provisional).toBe(true)
    expect(listing.officiallyClosed).toBe(false)
    expect(listing.mayIssueBallots).toBe(false)
  })

  it('blocks suffrage for any workshop with a pending balance until Régimen Interior lifts it', () => {
    const store = new MockAssemblyStore()
    const before = store.listing(ID)
    const indebted = before.talleresConBloqueo[0]
    expect(indebted.blockLifted).toBe(false)
    expect(before.sufragantes.some(v => v.workshop === indebted.workshop)).toBe(false)

    store.decideDebtBlock(indebted.organizationId, { action: 'lift', reason: 'Convenio de pago acreditado', evidenceReference: 'ACTA-RI-1' })
    const lifted = store.listing(ID)
    expect(lifted.talleresConBloqueo[0].blockLifted).toBe(true)
    expect(lifted.sufragantes.filter(v => v.workshop === indebted.workshop)).toHaveLength(indebted.affected)

    store.decideDebtBlock(indebted.organizationId, { action: 'revoke', reason: 'Convenio incumplido por el Taller', evidenceReference: 'ACTA-RI-2' })
    expect(store.listing(ID).sufragantes.some(v => v.workshop === indebted.workshop)).toBe(false)
  })

  it('requires a real reason and reference to lift a block', () => {
    const store = new MockAssemblyStore()
    expect(() => store.decideDebtBlock('demo-org-23', { action: 'lift', reason: 'corto', evidenceReference: 'x' })).toThrow()
    expect(() => store.decideDebtBlock('demo-org-23', { action: 'lift', reason: 'Motivo suficiente', evidenceReference: '' })).toThrow()
  })

  it('creates an empty preliminary assembly and rejects a cutoff after the meeting', () => {
    const store = new MockAssemblyStore()
    expect(() => store.create({ name: 'X', meetingDate: '2026-11-01', cutoffDate: '2026-11-02' })).toThrow()
    const created = store.create({ name: 'Asamblea extraordinaria', meetingDate: '2026-12-01', cutoffDate: '2026-11-20' })
    expect(store.listing(created.id).asambleistas).toHaveLength(0)
    expect(store.list().items[0].id).toBe(created.id)
  })

  it('labels never claim a confirmed vote for pending sources', () => {
    expect(assemblyStatusLabel({ status: 'pendiente', blockedByDebt: false, blockLifted: false, mayVote: false })).toContain('Pendiente')
    expect(assemblyStatusLabel({ status: 'solo_asistencia', blockedByDebt: true, blockLifted: false, mayVote: false })).toContain('saldo pendiente')
  })
})
