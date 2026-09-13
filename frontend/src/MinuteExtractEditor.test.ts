import { describe, expect, it } from 'vitest'
import { buildCompletedMinuteExtract } from './MinuteExtractEditor'

describe('Extracto de Acta 2026', () => {
  it('preserves automated attendance and ballots while completing narrative fields', () => {
    const result = buildCompletedMinuteExtract('EXTRACTO DE ACTA\nASISTENCIA\nPresentes: 12\nBALOTAJE Y VOTACIONES\nPrimer trámite · blancas 12; negras 0.\nApertura: __________', {
      openingTime: '19:30', previousMinuteDate: '2026-09-05', previousMinuteApproved: 'Sí', correspondence: 'Circular 12', decrees: '104, 105', proposalBag: 'Sin proposiciones', workAuthor: 'Persona Demostrativa', workTitle: 'La fraternidad', contributions: 'Integrante Uno', generalGood: 'Integrante Dos', charityAmount: '18500', closingTime: '22:15', chainCloser: 'Integrante Tres',
    })
    expect(result).toContain('Presentes: 12')
    expect(result).toContain('Primer trámite · blancas 12; negras 0.')
    expect(result).toContain('Apertura: 19:30 horas')
    expect(result).toContain('Tronco de beneficencia: $ 18.500 m.p.')
    expect(result).toContain('Venerable Maestro/a: ____________________')
  })
})
