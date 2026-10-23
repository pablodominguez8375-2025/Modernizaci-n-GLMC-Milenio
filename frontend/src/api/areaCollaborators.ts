export type CollaboratorArea = 'library' | 'documentmanager'
export type CollaboratorAction = 'view' | 'create' | 'write' | 'edit' | 'delete'
export interface AreaCollaborator { assignmentId: string; subject: string; area: CollaboratorArea; category: string; actions: CollaboratorAction[]; effectiveFrom: string; effectiveTo: string | null; isActive: boolean }
export interface AreaCollaborators { version: number; items: AreaCollaborator[] }
export interface AreaCollaboratorCandidates { available: boolean; items: Array<{ subject: string; name: string }> }
export interface AddAreaCollaborator { subject: string; category: string; actions: CollaboratorAction[]; effectiveFrom: string; effectiveTo: string | null; expectedVersion: number }

export const collaboratorCategories: Record<CollaboratorArea, Array<{ value: string; label: string }>> = {
  library: [
    { value: 'magazine', label: 'Revista Horizonte Masónico' }, { value: 'bulletin', label: 'Boletín mensual' }, { value: 'book', label: 'Libros' },
    { value: 'work_paper', label: 'Planchas de Trabajo' }, { value: 'official_document', label: 'Documentos oficiales' }, { value: 'video', label: 'Videos' },
  ],
  documentmanager: [
    { value: 'official_document', label: 'Documentos oficiales' }, { value: 'book', label: 'Libros' }, { value: 'work_paper', label: 'Planchas de Trabajo' },
    { value: 'magazine', label: 'Revistas' }, { value: 'bulletin', label: 'Boletines' }, { value: 'video', label: 'Videos' },
  ],
}
export const collaboratorActionLabels: Record<CollaboratorAction, string> = { view: 'Ver', create: 'Crear borradores', write: 'Subir y publicar contenido', edit: 'Editar', delete: 'Retirar' }
/** El permiso de subir contenido solo se delega para Revista y Boletín de la Biblioteca (regla del servidor). */
export const canDelegateUpload = (area: CollaboratorArea, category: string) => area === 'library' && (category === 'magazine' || category === 'bulletin')

/** Demostración con datos ficticios: imita las reglas del servidor sin tocar ninguna cuenta real. */
export class AreaCollaboratorsDemo {
  private version = 7
  private items: AreaCollaborator[] = [
    { assignmentId: 'demo-collab-1', subject: 'demo:communications', area: 'library', category: 'magazine', actions: ['view', 'create', 'write'], effectiveFrom: '2026-09-01', effectiveTo: '2026-12-31', isActive: true },
    { assignmentId: 'demo-collab-2', subject: 'demo:communications', area: 'library', category: 'bulletin', actions: ['view', 'create', 'write'], effectiveFrom: '2026-09-01', effectiveTo: null, isActive: true },
  ]
  candidates(): AreaCollaboratorCandidates {
    return { available: true, items: [
      { subject: 'demo:communications', name: 'Comunicaciones · Demostración' }, { subject: 'demo:brother', name: 'Hermano ficticio · Demostración' },
      { subject: 'demo:lodgeSecretary', name: 'Secretario de Taller · Demostración' }, { subject: 'demo:treasury', name: 'Tesorero ficticio · Demostración' },
    ] }
  }
  list(area: CollaboratorArea): AreaCollaborators { return { version: this.version, items: structuredClone(this.items.filter(i => i.area === area && i.isActive)) } }
  add(area: CollaboratorArea, request: AddAreaCollaborator): AreaCollaborators {
    const subject = request.subject.trim()
    if (!subject || !request.actions.includes('view') || (request.effectiveTo && request.effectiveTo < request.effectiveFrom)) throw new Error('Revise el usuario, las acciones y las fechas.')
    if (request.actions.includes('write') && !canDelegateUpload(area, request.category)) throw new Error('Subir contenido solo se delega para Revista y Boletín de la Biblioteca.')
    if (request.expectedVersion !== this.version) throw new Error('La lista cambió mientras la editaba. Se recargó; revise y repita.')
    const end = request.effectiveTo ?? '9999-12-31'
    if (this.items.some(i => i.isActive && i.area === area && i.subject === subject && i.category === request.category && i.effectiveFrom <= end && (i.effectiveTo ?? '9999-12-31') >= request.effectiveFrom)) throw new Error('Ese usuario ya tiene acceso vigente a esta categoría en ese período.')
    this.items.push({ assignmentId: `demo-collab-${Date.now()}`, subject, area, category: request.category, actions: request.actions, effectiveFrom: request.effectiveFrom, effectiveTo: request.effectiveTo, isActive: true })
    this.version += 1
    return this.list(area)
  }
  revoke(area: CollaboratorArea, assignmentId: string, expectedVersion: number): AreaCollaborators {
    if (expectedVersion !== this.version) throw new Error('La lista cambió mientras la editaba. Se recargó; revise y repita.')
    const found = this.items.find(i => i.assignmentId === assignmentId && i.area === area && i.isActive)
    if (!found) throw new Error('El acceso indicado no existe en esta área.')
    found.isActive = false; this.version += 1
    return this.list(area)
  }
}
