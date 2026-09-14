import { describe, expect, it } from 'vitest'
import { candidateCoreFields, candidateDemoData } from './CandidateProfilePage'

describe('CandidateProfilePage contract', () => {
  it('keeps the agreed identity and lodge fields in the core', () => {
    expect(candidateCoreFields).toContain('passportPhoto')
    expect(candidateCoreFields).toContain('names')
    expect(candidateCoreFields).toContain('paternalSurname')
    expect(candidateCoreFields).toContain('maternalSurname')
    expect(candidateCoreFields).toContain('presentingLodge')
    expect(candidateCoreFields).toContain('processStatus')
  })

  it('contains the operational process information required for the demo', () => {
    expect(candidateDemoData.presenters.length).toBeGreaterThan(0)
    expect(candidateDemoData.documents.length).toBeGreaterThanOrEqual(5)
    expect(candidateDemoData.history.length).toBeGreaterThanOrEqual(5)
  })

  it('uses explicitly fictitious public showcase data', () => {
    expect(candidateDemoData.rut).toContain('DEMO')
    expect(candidateDemoData.email).toContain('ejemplo.cl')
    expect(candidateDemoData.presentingLodge).toContain('Demostrativo')
  })
})
