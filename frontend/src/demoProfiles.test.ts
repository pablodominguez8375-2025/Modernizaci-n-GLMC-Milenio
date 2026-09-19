import { describe, expect, it } from 'vitest'
import { getDemoProfile } from './demoProfiles'

describe('showcase role profiles', () => {
  it('keeps the brother profile limited to personal and degree-based access', () => {
    const profile = getDemoProfile('brother')
    expect(profile.accessScope).toBe('authenticated')
    expect(profile.capabilities.canReadLibrary).toBe(true)
    expect(profile.capabilities.canManageLodgeOperations).toBe(false)
    expect(profile.capabilities.canManageGrandSecretariat).toBe(false)
    expect(profile.capabilities.canRunRegimenInteriorReports).toBe(false)
    expect(profile.capabilities.canManageGrandArchive).toBe(false)
  })

  it('gives a lodge authority operational Taller access without Gran Logia authority', () => {
    const profile = getDemoProfile('lodge')
    expect(profile.displayName).toBe('Venerable Maestro · Demostración')
    expect(profile.accessScope).toBe('organization')
    expect(profile.capabilities.canManageLodgeOperations).toBe(true)
    expect(profile.capabilities.canManageDocuments).toBe(true)
    expect(profile.capabilities.canManageGrandSecretariat).toBe(false)
    expect(profile.capabilities.canManageTreasuryRegularity).toBe(false)
  })

  it('exposes each administrative and teaching Taller role in the QA switcher model', () => {
    for (const key of ['lodgeTreasurer', 'lodgeSecretary', 'lodgeOrator', 'lodgeFirstWarden', 'lodgeSecondWarden', 'lodgePastMaster'] as const) {
      const profile = getDemoProfile(key)
      expect(profile.accessScope).toBe('organization')
      expect(profile.capabilities.canManageLodgeOperations).toBe(true)
    }
  })

  it('separates Hospitalaria management from Venerable inspection and approval', () => {
    const hospitalario = getDemoProfile('lodgeHospitalaria')
    expect(hospitalario.capabilities.canManageLodgeOperations).toBe(false)
    expect(hospitalario.capabilities.canReadLodgeHospitalaria).toBe(true)
    expect(hospitalario.capabilities.canManageLodgeHospitalaria).toBe(true)
    expect(hospitalario.capabilities.canApproveLodgeExpenses).not.toBe(true)
    expect(hospitalario.capabilities.canManageHospitalariaRegularity).toBe(false)

    const venerable = getDemoProfile('lodge')
    expect(venerable.capabilities.canReadLodgeHospitalaria).toBe(true)
    expect(venerable.capabilities.canManageLodgeHospitalaria).not.toBe(true)
    expect(venerable.capabilities.canApproveLodgeExpenses).toBe(true)

    const granHospitalaria = getDemoProfile('hospitalaria')
    expect(granHospitalaria.capabilities.canManageHospitalariaRegularity).toBe(true)
    expect(granHospitalaria.capabilities.canReadLodgeHospitalaria).not.toBe(true)
  })

  it('gives the lodge treasurer the monthly-statement capability without Grand Treasury authority', () => {
    const profile = getDemoProfile('lodgeTreasurer')
    expect(profile.capabilities.canManageLodgeTreasury).toBe(true)
    expect(profile.capabilities.canManageTreasuryRegularity).toBe(false)
  })

  it('gives a Gran Logia authority order-level institutional capabilities', () => {
    const profile = getDemoProfile('grandLodge')
    expect(profile.accessScope).toBe('order')
    expect(profile.capabilities.canManageGrandSecretariat).toBe(true)
    expect(profile.capabilities.canRunRegimenInteriorReports).toBe(true)
    expect(profile.capabilities.canManageTreasuryRegularity).toBe(true)
    expect(profile.capabilities.canManageHospitalariaRegularity).toBe(true)
    expect(profile.capabilities.canManageGrandArchive).toBe(true)
    expect(profile.capabilities.canBootstrapInstitutional).toBe(true)
    expect(profile.capabilities.canConfigureSystem).toBe(true)
  })

  it('provides a dedicated system administrator profile', () => {
    const profile = getDemoProfile('systemAdmin')
    expect(profile.displayName).toBe('Administrador del Sistema · Demostración')
    expect(profile.capabilities.canConfigureSystem).toBe(true)
  })
})
