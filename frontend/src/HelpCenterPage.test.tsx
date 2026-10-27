import { describe, expect, it } from 'vitest'
import { renderToStaticMarkup } from 'react-dom/server'
import HelpCenterPage from './HelpCenterPage'
import type { HelpAccess } from './help/helpGuides'

const base: HelpAccess = {
  everyone: true, member: true, calendar: true, notifications: true, lodgeSecretariat: false, grandSecretariat: false,
  systemAdmin: false, supportView: false, library: false, regimen: false,
}

describe('Centro de ayuda', () => {
  it('muestra el buscador y los temas con sus guías', () => {
    const html = renderToStaticMarkup(<HelpCenterPage access={base} />)
    expect(html).toContain('¿En qué podemos ayudarte?')
    expect(html).toContain('Buscar en la ayuda')
    expect(html).toContain('Primeros pasos')
    expect(html).toContain('Ver mis pagos y mi cartola')
  })

  it('oculta las guías de cargos que la persona no tiene', () => {
    const html = renderToStaticMarkup(<HelpCenterPage access={base} />)
    expect(html).not.toContain('Pedir un templo o una sala')
    expect(html).not.toContain('Aprobar o rechazar una solicitud')
    expect(html).not.toContain('Ayudar a alguien que no puede ingresar')
  })

  it('la Secretaría del Taller ve la guía de pedir un templo', () => {
    const html = renderToStaticMarkup(<HelpCenterPage access={{ ...base, lodgeSecretariat: true }} />)
    expect(html).toContain('Pedir un templo o una sala a la Gran Secretaría')
    expect(html).toContain('Mi Taller')
  })
})
