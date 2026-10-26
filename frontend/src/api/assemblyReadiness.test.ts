import { describe, expect, it } from 'vitest'
import { MockAssemblyStore, assemblyReadinessStateLabel } from './assemblyApi'

describe('informe «Qué falta para cerrar el padrón» (demo)', () => {
  it('nunca habilita el cierre y explica cada fuente pendiente', () => {
    const store = new MockAssemblyStore()
    const r = store.readiness('demo-asamblea-2026')
    expect(r.mayClose).toBe(false)
    expect(r.provisional).toBe(true)
    expect(r.items.map(i => i.code)).toEqual(['assembly', 'master_terms', 'roster', 'evaluation', 'grand_treasury', 'rights', 'closure'])
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
})
