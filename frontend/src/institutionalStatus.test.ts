import { describe, expect, it } from 'vitest'
import { institutionalStatusLabel, isPastActiveStatus, isVoluntaryWithdrawalStatus } from './institutionalStatus'

describe('institutional status semantics', () => {
  it('keeps Past Activo distinct from Retiro voluntario / En sueño', () => {
    expect(institutionalStatusLabel('past_active')).toBe('Past Activo')
    expect(institutionalStatusLabel('voluntary_withdrawal')).toBe('Retiro voluntario / En sueño')
    expect(institutionalStatusLabel('past_active')).not.toBe(institutionalStatusLabel('voluntary_withdrawal'))
  })

  it('never classifies En sueño as Past Activo', () => {
    expect(isPastActiveStatus('past_active')).toBe(true)
    expect(isPastActiveStatus('voluntary_withdrawal')).toBe(false)
    expect(isVoluntaryWithdrawalStatus('voluntary_withdrawal')).toBe(true)
    expect(isVoluntaryWithdrawalStatus('past_active')).toBe(false)
  })

  it('presents workshop transfer as continued active status', () => {
    expect(institutionalStatusLabel('workshop_transfer')).toBe('Activo')
  })
})
