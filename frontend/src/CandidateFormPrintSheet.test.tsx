import { readFileSync } from 'node:fs'
import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { CandidateFormPrintContent } from './CandidateFormPrintSheet'
import type { CandidateIntakeProfile } from './api/candidateIntakeApi'

const profile: CandidateIntakeProfile = {
  ceremonyRequestId: 'r1', firstNames: 'Ana María', paternalSurname: 'Pérez', maternalSurname: 'Soto',
  rutOrInstitutionalId: '11.111.111-1', birthDate: '1990-03-05', nationality: 'Chilena', civilStatus: 'Soltera',
  occupation: 'Ingeniera', employerName: null, workAddress: null, workPosition: null, workPhone: null,
  phone: '+56 9 1111 2222', email: 'ana@example.cl', address: 'Calle 1', city: 'Santiago',
  workshopName: 'Taller Ejemplo', workshopNumber: '12', orient: 'Santiago', presenters: ['Hermano Uno'],
  insinuationDate: '2026-10-01', firstDegreePresentationDate: null, responsibleSecretaryName: 'Secretaria Ejemplo',
  reviewStatus: 'pending', photoAvailable: false, completenessPercent: 80, missingRequirements: [],
  interviewSummary: 'RESUMEN RESERVADO', internalObservations: 'OBSERVACION INTERNA',
  submittedAtUtc: '2026-10-01T00:00:00Z', updatedAtUtc: '2026-10-01T00:00:00Z',
}

describe('Hoja de impresión de la ficha del insinuado', () => {
  it('muestra los datos de la ficha y deja guiones en lo que falta', () => {
    const text = renderToStaticMarkup(<CandidateFormPrintContent profile={profile} photoSrc={null} originText="Redes sociales" sponsorText={null} />)
    expect(text).toContain('Ana María Pérez Soto')
    expect(text).toContain('11.111.111-1')
    expect(text).toContain('05-03-1990')
    expect(text).toContain('Taller Ejemplo N° 12')
    expect(text).not.toContain('N° 12 N° 12')
    expect(text).toContain('Redes sociales')
    expect(text).toContain('Firma Secretaría del Taller')
    expect(text).toContain('Fotografía tipo pasaporte')
  })

  it('no incluye observaciones internas ni el resumen de entrevista', () => {
    const text = renderToStaticMarkup(<CandidateFormPrintContent profile={profile} photoSrc={null} originText="Sin registrar" sponsorText={null} />)
    expect(text).not.toContain('OBSERVACION INTERNA')
    expect(text).not.toContain('RESUMEN RESERVADO')
  })

  it('la Secretaría del Taller tiene botón Imprimir ficha y la hoja se oculta en pantalla', () => {
    expect(readFileSync(new URL('./CandidateWorkshopIntakePage.tsx', import.meta.url), 'utf8')).toContain('Imprimir ficha')
    const css = readFileSync(new URL('./candidate-print.css', import.meta.url), 'utf8')
    expect(css).toMatch(/\.candidate-print-sheet \{ display: none; \}/)
    expect(css).toContain('@media print')
  })
})
