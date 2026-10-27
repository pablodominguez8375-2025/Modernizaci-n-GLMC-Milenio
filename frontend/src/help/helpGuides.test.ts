import { describe, expect, it } from 'vitest'
import { helpGuides, helpTopics, searchGuides, visibleGuides, type HelpAccess } from './helpGuides'

const nobody: HelpAccess = {
  everyone: true, member: false, calendar: false, notifications: false, lodgeSecretariat: false, grandSecretariat: false,
  systemAdmin: false, supportView: false, library: false, regimen: false,
}

describe('Centro de ayuda · guías', () => {
  it('cada guía tiene tema conocido, pasos y un id único', () => {
    const ids = new Set<string>()
    for (const guide of helpGuides) {
      expect(helpTopics).toContain(guide.topic)
      expect(guide.steps.length).toBeGreaterThanOrEqual(2)
      expect(ids.has(guide.id)).toBe(false)
      ids.add(guide.id)
    }
  })

  it('sin cargo solo se ven las guías generales', () => {
    const visible = visibleGuides(nobody)
    expect(visible.length).toBeGreaterThan(0)
    expect(visible.every(guide => guide.needs === 'everyone')).toBe(true)
  })

  it('la guía de pedir templo solo la ve la Secretaría del Taller y la de responder solo la Gran Secretaría', () => {
    expect(visibleGuides(nobody).some(guide => guide.id === 'pedir-espacio')).toBe(false)
    expect(visibleGuides({ ...nobody, lodgeSecretariat: true }).some(guide => guide.id === 'pedir-espacio')).toBe(true)
    expect(visibleGuides({ ...nobody, lodgeSecretariat: true }).some(guide => guide.id === 'responder-espacio')).toBe(false)
    expect(visibleGuides({ ...nobody, grandSecretariat: true }).some(guide => guide.id === 'responder-espacio')).toBe(true)
  })

  it('la guía de templos no pide firma del Venerable Maestro ni plazo de 7 días ni equipos', () => {
    const guide = helpGuides.find(item => item.id === 'pedir-espacio')!
    const text = `${guide.summary} ${guide.steps.join(' ')} ${guide.tip ?? ''}`.toLowerCase()
    expect(text).not.toContain('7 días')
    expect(text).not.toContain('equipo')
    expect(guide.summary).toContain('No se necesita la firma del Venerable Maestro')
  })

  it('el buscador ignora tildes y mayúsculas y exige todas las palabras', () => {
    const all = visibleGuides({ ...nobody, member: true })
    expect(searchGuides(all, 'PAGOS').map(guide => guide.id)).toContain('pagos')
    expect(searchGuides(all, 'asistencia tenidas').map(guide => guide.id)).toContain('asistencias')
    expect(searchGuides(all, 'cartola zzz')).toHaveLength(0)
    expect(searchGuides(all, '   ')).toHaveLength(all.length)
  })

  it('ninguna guía incluye datos personales ni correos', () => {
    const text = JSON.stringify(helpGuides)
    expect(text).not.toMatch(/@[a-z0-9-]+\.[a-z]/i)
    expect(text).not.toMatch(/\d{1,2}\.\d{3}\.\d{3}-[\dkK]/)
  })
})
