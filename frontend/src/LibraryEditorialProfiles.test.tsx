import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import LibraryManagement, { type LibraryCategory } from './LibraryManagement'
import LibraryPage from './LibraryPage'
import { DocumentApiClient } from './api/documentApi'
import { getDemoProfile } from './demoProfiles'

const categories: LibraryCategory[] = [
  { value: 'work_paper', label: 'Planchas de Trabajo', base: true, active: true },
  { value: 'magazine', label: 'Revistas', base: true, active: true },
  { value: 'bulletin', label: 'Boletines', base: true, active: true },
]
const props = { categories, onCategories: () => {}, counts: {}, submissions: [], onSubmission: () => {}, onPublish: () => {}, collectionId: 'c', notify: () => {} }

describe('perfiles institucionales Bibliotecario y Comunicaciones (#635)', () => {
  it('declara ambos perfiles en la demo con capacidades separadas y sin Gestión Documental', () => {
    const librarian = getDemoProfile('librarian')
    expect(librarian.displayName).toBe('Bibliotecario · Demostración')
    expect(librarian.accessScope).toBe('order')
    expect(librarian.capabilities.canManageLibrary).toBe(true)
    expect(librarian.capabilities.canManageCommunications).toBe(false)
    expect(librarian.capabilities.canManageDocuments).toBe(false)
    expect(librarian.capabilities.canManageGrandArchive).toBe(false)

    const communications = getDemoProfile('communications')
    expect(communications.displayName).toBe('Comunicaciones · Demostración')
    expect(communications.accessScope).toBe('order')
    expect(communications.capabilities.canManageCommunications).toBe(true)
    expect(communications.capabilities.canManageLibrary).toBe(false)
    expect(communications.capabilities.canManageDocuments).toBe(false)
    expect(communications.capabilities.canConfigureSystem).not.toBe(true)

    expect(getDemoProfile('brother').capabilities.canManageLibrary).not.toBe(true)
    expect(getDemoProfile('grandArchivist').capabilities.canManageCommunications).not.toBe(true)
  })

  it('Comunicaciones sólo sube revista o boletín, sin categorías ni planchas por revisar', () => {
    const html = renderToStaticMarkup(<LibraryManagement {...props} mode="communications" />)
    expect(html).toContain('Subir revista o boletín')
    expect(html).toContain('Mis borradores')
    expect(html).not.toContain('Administrar categorías')
    expect(html).not.toContain('Por revisar y borradores')
  })

  it('el Bibliotecario conserva la administración completa de la Biblioteca', () => {
    const html = renderToStaticMarkup(<LibraryManagement {...props} mode="librarian" />)
    expect(html).toContain('Subir a la biblioteca')
    expect(html).toContain('Por revisar y borradores')
    expect(html).toContain('Administrar categorías')
  })

  it('la demo sólo muestra herramientas de carga a un perfil editorial y ofrece la categoría Boletines', () => {
    const documentApi = new DocumentApiClient({ useMocks: true })
    const reader = renderToStaticMarkup(<LibraryPage documentApi={documentApi} />)
    expect(reader).toContain('Boletines')
    expect(reader).not.toContain('Subir a la biblioteca')
    expect(reader).not.toContain('Subir revista o boletín')
    expect(renderToStaticMarkup(<LibraryPage documentApi={documentApi} editorRole="communications" />)).toContain('Subir revista o boletín')
    expect(renderToStaticMarkup(<LibraryPage documentApi={documentApi} editorRole="librarian" />)).toContain('Subir a la biblioteca')
  })
})
