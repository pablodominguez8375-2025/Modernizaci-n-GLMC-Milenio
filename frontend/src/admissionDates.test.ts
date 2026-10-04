import { describe, expect, it } from 'vitest'
import { affiliationModeForDate, chileCivilDate } from './admissionDates'

describe('Carta de Retiro Voluntario: tres meses calendario', () => {
  it.each([
    ['2026-07-03', '2026-10-03', 'simple'],
    ['2026-07-03', '2026-10-04', 'activation'],
    ['2026-01-31', '2026-04-30', 'simple'],
    ['2026-01-31', '2026-05-01', 'activation'],
    ['2025-11-30', '2026-02-28', 'simple'],
    ['2025-11-30', '2026-03-01', 'activation'],
    ['2023-11-30', '2024-02-29', 'simple'],
    ['2023-11-30', '2024-03-01', 'activation'],
    ['2026-10-03', '2026-10-03', 'simple'],
    ['2026-10-04', '2026-10-03', null],
    ['2026-02-30', '2026-10-03', null],
    ['', '2026-10-03', null],
  ])('%s evaluada en %s => %s', (granted, today, expected) => {
    expect(affiliationModeForDate(granted, today)).toBe(expected)
  })
  it('usa el día de Chile, no el del navegador o UTC', () => {
    expect(chileCivilDate(new Date('2026-10-04T01:00:00Z'))).toBe('2026-10-03')
    expect(chileCivilDate(new Date('2026-10-04T04:00:00Z'))).toBe('2026-10-04')
  })
})
