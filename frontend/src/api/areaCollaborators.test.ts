import { describe, expect, it } from 'vitest'
import { DocumentApiClient } from './documentApi'

describe('colaboradores por área', () => {
  const api = () => new DocumentApiClient({ useMocks: true })
  const base = { subject: 'sujeto-ficticio', category: 'magazine', actions: ['view', 'write'] as const, effectiveFrom: '2026-10-01', effectiveTo: null }
  it('otorga y revoca con control de versión', async () => {
    const c = api(); const first = await c.getAreaCollaborators('library')
    const added = await c.addAreaCollaborator('library', { ...base, actions: ['view', 'write'], expectedVersion: first.version })
    expect(added.version).toBe(first.version + 1); expect(added.items.some(i => i.subject === 'sujeto-ficticio')).toBe(true)
    await expect(c.addAreaCollaborator('library', { ...base, actions: ['view'], category: 'book', expectedVersion: first.version })).rejects.toThrow('cambió')
    const target = added.items.find(i => i.subject === 'sujeto-ficticio')!
    const after = await c.revokeAreaCollaborator('library', target.assignmentId, added.version)
    expect(after.items.some(i => i.assignmentId === target.assignmentId)).toBe(false)
  })
  it('subir contenido solo para Revista y Boletín de la Biblioteca; no cruza áreas ni se superpone', async () => {
    const c = api(); const v = (await c.getAreaCollaborators('library')).version
    await expect(c.addAreaCollaborator('library', { ...base, category: 'book', actions: ['view', 'write'], expectedVersion: v })).rejects.toThrow('Revista y Boletín')
    await expect(c.addAreaCollaborator('documentmanager', { ...base, actions: ['view', 'write'], expectedVersion: v })).rejects.toThrow('Revista y Boletín')
    await expect(c.addAreaCollaborator('library', { ...base, actions: ['edit'], expectedVersion: v })).rejects.toThrow('Revise')
    await expect(c.addAreaCollaborator('library', { ...base, subject: 'demo:communications', actions: ['view'], expectedVersion: v })).rejects.toThrow('ya tiene acceso')
    expect((await c.getAreaCollaborators('documentmanager')).items).toHaveLength(0)
  })
})
