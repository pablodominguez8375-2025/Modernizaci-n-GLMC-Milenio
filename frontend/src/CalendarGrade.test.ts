import { describe, expect, it } from 'vitest'
import { CalendarApiClient } from './api/calendarApi'
import { gradeLabel, scopeSummary } from './CalendarPage'

const window = { fromUtc: new Date(Date.now() - 86_400_000).toISOString(), toUtc: new Date(Date.now() + 60 * 86_400_000).toISOString() }

async function titles(viewer: Parameters<CalendarApiClient['setDemoViewer']>[0]) {
  const api = new CalendarApiClient({ useMocks: true })
  api.setDemoViewer(viewer)
  return (await api.getCalendar(window)).events.map(event => event.title)
}

describe('Agenda por grado (demo y etiquetas)', () => {
  it('Aprendiz ve su grado y lo general; no ve Compañeros ni Maestros', async () => {
    const list = await titles('apprentice')
    expect(list).toContain('Docencia de Aprendices')
    expect(list).toContain('Tenida Regular — Libertad Nº 23')
    expect(list).not.toContain('Docencia de Compañeros — Segundo viaje')
    expect(list).not.toContain('Docencia de Maestros')
  })
  it('Compañero ve 2° y 1°; Maestro ve 3°, 2° y 1°; el cargo vigente ve todo', async () => {
    const fellow = await titles('fellow_craft')
    expect(fellow).not.toContain('Docencia de Aprendices')
    expect(fellow).toContain('Docencia de Compañeros — Segundo viaje')
    expect(fellow).not.toContain('Docencia de Maestros')
    const master = await titles('master')
    expect(master).toContain('Docencia de Maestros')
    expect(master).not.toContain('Docencia de Aprendices')
    expect(master).not.toContain('Docencia de Compañeros — Segundo viaje')
    expect((await titles('office')).length).toBeGreaterThan(master.length)
  })
  it('etiquetas simples y resumen de alcance', () => {
    expect(gradeLabel('apprentice')).toBe('Aprendices (1°)')
    expect(gradeLabel('fellow_craft')).toBe('Compañeros (2°)')
    expect(gradeLabel('master')).toBe('Maestros (3°)')
    expect(gradeLabel(null)).toBe('General')
    expect(gradeLabel('all')).toBe('General')
    expect(scopeSummary({ lodge: 'T', degree: 'Compañero (2°)', office: null })).toBe('Mostrando: tu grado y los inferiores, más lo general. Las instrucciones: solo las de tu grado')
    expect(scopeSummary({ lodge: 'T', degree: 'Maestro (3°)', office: 'Secretario' })).toBe('Mostrando: todos los grados de tu Taller')
    expect(scopeSummary(null)).toBeNull()
  })
})
