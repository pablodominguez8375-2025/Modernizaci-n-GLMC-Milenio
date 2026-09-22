import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it, vi } from 'vitest'
import SecretariatRoleNavigation from './SecretariatRoleNavigation'

describe('SecretariatRoleNavigation', () => {
  it('agrupa la operación de Secretaría del Taller por función', () => {
    const html = renderToStaticMarkup(<SecretariatRoleNavigation title="Secretaría" active="lodge" onChange={vi.fn()} sections={[
      { id: 'lodge', label: 'Tenidas y actas', description: 'Asistencia, extractos y correspondencia' },
      { id: 'candidateProfile', label: 'Insinuados', description: 'Alta y seguimiento del expediente' },
      { id: 'initiationCircuit', label: 'Circuito de iniciación', description: 'Requisitos y solicitud de Plancha' },
    ]} />)
    for (const label of ['Secretaría','Tenidas y actas','Insinuados','Circuito de iniciación']) expect(html).toContain(label)
    expect(html).toContain('aria-current="page"')
  })

  it('agrupa las bandejas de Gran Secretaría', () => {
    const html = renderToStaticMarkup(<SecretariatRoleNavigation title="Gran Secretaría" active="secretariat" onChange={vi.fn()} sections={[
      { id: 'secretariat', label: 'Bandeja institucional', description: 'Planchas PDF, extractos y espacios' },
      { id: 'ceremonies', label: 'Ceremonias', description: 'Autorizaciones institucionales' },
    ]} />)
    expect(html).toContain('Bandeja institucional')
    expect(html).toContain('Ceremonias')
  })
})
