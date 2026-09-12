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
    expect(profile.accessScope).toBe('organization')
    expect(profile.capabilities.canManageLodgeOperations).toBe(true)
    expect(profile.capabilities.canManageDocuments).toBe(true)
    expect(profile.capabilities.canManageGrandSecretariat).toBe(false)
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
  })
})
