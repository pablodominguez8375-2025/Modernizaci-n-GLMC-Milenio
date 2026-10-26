import { describe, expect, it } from 'vitest'
import { MockAssemblyStore, assemblyReadinessStateLabel } from './assemblyApi'

describe('informe «Qué falta para cerrar el padrón» (demo)', () => {
  it('nunca habilita el cierre y explica cada fuente pendiente', () => {
    const store = new MockAssemblyStore()
    const r = store.readiness('demo-asamblea-2026')
    expect(r.mayClose).toBe(false)
    expect(r.provisional).toBe(true)
    expect(r.items.map(i => i.code)).toEqual(['assembly', 'master_terms', 'membership', 'roster', 'evaluation', 'grand_treasury', 'rights', 'closure'])
    expect(r.items.find(i => i.code === 'closure')?.state).toBe('blocked')
    expect(r.blockingCodes).toContain('closure')
  })
  it('una Asamblea nueva bloquea los Venerables Maestros hasta la carga inicial', () => {
    const store = new MockAssemblyStore()
    const created = store.create({ name: 'Asamblea nueva', meetingDate: '2026-12-05', cutoffDate: '2026-11-30' })
    const r = store.readiness(created.id)
    expect(r.items.find(i => i.code === 'master_terms')?.state).toBe('blocked')
    expect(r.items.find(i => i.code === 'roster')?.state).toBe('pending')
  })
  it('usa etiquetas simples', () => {
    expect(assemblyReadinessStateLabel).toEqual({ ok: 'Listo', pending: 'Pendiente', blocked: 'Bloqueado' })
  })
  it('falla con una Asamblea inexistente', () => {
    expect(() => new MockAssemblyStore().readiness('no-existe')).toThrow('No existe la Asamblea')
  })
  it('las constancias de Régimen Interior marcan listo su paso y se pueden retirar, sin habilitar el cierre', () => {
    const store = new MockAssemblyStore()
    const id = 'demo-asamblea-2026'
    const step = (code: string) => store.readiness(id).items.find(i => i.code === code)?.state
    expect(store.attestationList(id).items.every(a => !a.attested)).toBe(true)
    store.recordAttestation(id, { kind: 'master_periods', action: 'attest', actaReference: 'ACTA-1', statement: 'Períodos verificados con las actas.' })
    expect(step('master_terms')).toBe('ok')
    store.recordAttestation(id, { kind: 'honor_coverage', action: 'attest', actaReference: 'ACTA-2', statement: 'Cobertura completa del Tribunal.' })
    expect(step('rights')).toBe('pending')
    store.recordAttestation(id, { kind: 'regimen_coverage', action: 'attest', actaReference: 'ACTA-3', statement: 'Cobertura completa de Régimen.' })
    expect(step('rights')).toBe('ok')
    expect(store.readiness(id).mayClose).toBe(false)
    expect(() => store.recordAttestation(id, { kind: 'honor_coverage', action: 'attest', actaReference: 'A', statement: 'Declaración suficiente.' })).toThrow('ya está vigente')
    store.recordAttestation(id, { kind: 'honor_coverage', action: 'revoke', actaReference: 'ACTA-4', statement: 'Se retira por corrección.' })
    expect(step('rights')).toBe('pending')
    expect(() => store.recordAttestation(id, { kind: 'membership', action: 'revoke', actaReference: 'A', statement: 'Declaración suficiente.' })).toThrow('No hay una constancia')
  })
  it('exige acta y declaración, y no certifica Asambleas sin asambleístas', () => {
    const store = new MockAssemblyStore()
    expect(() => store.recordAttestation('demo-asamblea-2026', { kind: 'membership', action: 'attest', actaReference: '', statement: 'Declaración suficiente.' })).toThrow('referencia del acta')
    const created = store.create({ name: 'Nueva', meetingDate: '2026-12-05', cutoffDate: '2026-11-30' })
    expect(() => store.recordAttestation(created.id, { kind: 'membership', action: 'attest', actaReference: 'A', statement: 'Declaración suficiente.' })).toThrow('nada que certificar')
  })
})
