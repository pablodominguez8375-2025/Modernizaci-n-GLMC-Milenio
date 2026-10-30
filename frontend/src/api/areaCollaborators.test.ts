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
    const existing = await c.addAreaCollaborator('library', { ...base, subject: 'demo:brother', actions: ['view'], expectedVersion: v })
    await expect(c.addAreaCollaborator('library', { ...base, subject: 'demo:brother', actions: ['view'], expectedVersion: existing.version })).rejects.toThrow('ya tiene acceso')
    expect((await c.getAreaCollaborators('documentmanager')).items).toHaveLength(0)
  })
  it('Comunicaciones crea y modifica solo sus colaboradores de Revista/Boletín sin cruzar Biblioteca', async () => {
    const c = api()
    const library = await c.getAreaCollaborators('library')
    const first = await c.getAreaCollaborators('communications')
    expect(first.items.every(i => i.area === 'communications')).toBe(true)
    expect(first.items.some(i => library.items.some(o => o.assignmentId === i.assignmentId))).toBe(false)
    await expect(c.addAreaCollaborator('communications', { ...base, category: 'book', actions: ['view'], expectedVersion: first.version })).rejects.toThrow('Revise')
    const added = await c.addAreaCollaborator('communications', { ...base, actions: ['view', 'write'], expectedVersion: first.version })
    const item = added.items.find(i => i.subject === base.subject)!
    expect(item.area).toBe('communications')
    await expect(c.updateAreaCollaborator('library', item.assignmentId, {
      ...base, actions: ['view', 'edit'], expectedVersion: added.version
    })).rejects.toThrow('no existe')
    await expect(c.updateAreaCollaborator('communications', item.assignmentId, {
      ...base, category: 'book', actions: ['view'], expectedVersion: added.version
    })).rejects.toThrow('Revise')
    const modified = await c.updateAreaCollaborator('communications', item.assignmentId, {
      ...base, category: 'bulletin', actions: ['view', 'edit'], expectedVersion: added.version
    })
    expect(modified.items.find(i => i.assignmentId === item.assignmentId)?.category).toBe('bulletin')
    expect(modified.items.find(i => i.assignmentId === item.assignmentId)?.actions).toEqual(['view', 'edit'])
    expect((await c.getAreaCollaborators('library')).items.some(i => i.assignmentId === item.assignmentId)).toBe(false)
    await expect(c.revokeAreaCollaborator('library', item.assignmentId, modified.version)).rejects.toThrow('no existe')
    const removed = await c.revokeAreaCollaborator('communications', item.assignmentId, modified.version)
    expect(removed.items.some(i => i.assignmentId === item.assignmentId)).toBe(false)
  })
  it('ofrece personas por nombre, sin correo ni datos de contacto', async () => {
    const r = await api().getAreaCollaboratorCandidates('library')
    expect(r.available).toBe(true); expect(r.items.length).toBeGreaterThan(0)
    expect(r.items.every(i => Object.keys(i).sort().join() === 'name,subject')).toBe(true)
  })
})
