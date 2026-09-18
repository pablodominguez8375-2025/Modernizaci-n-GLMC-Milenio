import type { SessionProfile } from './api/pmgmApi'

export type DemoProfileKey = 'brother' | 'lodge' | 'lodgeTreasurer' | 'lodgeSecretary' | 'lodgeHospitalaria' | 'lodgeOrator' | 'lodgeFirstWarden' | 'lodgeSecondWarden' | 'lodgePastMaster' | 'regimen' | 'treasury' | 'hospitalaria' | 'secretariat' | 'grandMaster' | 'systemAdmin' | 'grandLodge'

type ExtendedDemoCapabilities = SessionProfile['capabilities'] & {
  canBootstrapInstitutional?: boolean
  canManageLodgeOperations?: boolean
  canManageDocuments?: boolean
  canReadLibrary?: boolean
  canManageGrandArchive?: boolean
  canReadLodgeSecretariat?: boolean
  canManageLodgeSecretariat?: boolean
  canConfigureSystem?: boolean
}

export type DemoSessionProfile = Omit<SessionProfile, 'capabilities'> & {
  capabilities: ExtendedDemoCapabilities
}

const deniedCoreCapabilities: SessionProfile['capabilities'] = {
  canApproveTransfers: false,
  canRunRegimenInteriorReports: false,
  canManageGrandSecretariat: false,
  canManageTreasuryRegularity: false,
  canManageHospitalariaRegularity: false,
  canEvaluateCeremonies: false,
  canReviewCeremonies: false,
  canValidateCeremonyInternalAffairs: false,
  canAuthorizeCeremonies: false,
  canManagePrivacy: false,
  canReadLodgeSecretariat: false,
  canManageLodgeSecretariat: false,
}

export const demoProfiles: Record<DemoProfileKey, DemoSessionProfile> = {
  brother: {
    displayName: 'Hermano · Demostración',
    accessScope: 'authenticated',
    capabilities: {
      ...deniedCoreCapabilities,
      canReadLibrary: true,
      canManageLodgeOperations: false,
      canManageDocuments: false,
      canManageGrandArchive: false,
      canBootstrapInstitutional: false,
    },
  },
  lodge: {
    displayName: 'Venerable Maestro · Demostración',
    accessScope: 'organization',
    capabilities: {
      ...deniedCoreCapabilities,
      canReadLibrary: true,
      canManageLodgeOperations: true,
      canReadLodgeSecretariat: true,
      canManageLodgeSecretariat: false,
      canManageDocuments: true,
      canManageGrandArchive: false,
      canBootstrapInstitutional: false,
    },
  },
  lodgeTreasurer: {
    displayName: 'Tesorero del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true },
  },
  lodgeSecretary: {
    displayName: 'Secretaría del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canReadLodgeSecretariat: true, canManageLodgeSecretariat: true, canManageDocuments: true },
  },
  lodgeHospitalaria: {
    displayName: 'Hospitalaria del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true },
  },
  lodgeOrator: {
    displayName: 'Orador del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canReadLodgeSecretariat: true, canManageLodgeSecretariat: false, canManageDocuments: true },
  },
  lodgeFirstWarden: {
    displayName: 'Primer Vigilante · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true },
  },
  lodgeSecondWarden: {
    displayName: 'Segundo Vigilante · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true },
  },
  lodgePastMaster: {
    displayName: 'Ex Venerable Maestro · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true },
  },
  regimen: {
    displayName: 'Régimen Interior · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canRunRegimenInteriorReports: true, canReviewCeremonies: true, canValidateCeremonyInternalAffairs: true, canReadLibrary: true },
  },
  treasury: {
    displayName: 'Gran Tesorero · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canManageTreasuryRegularity: true, canReviewCeremonies: true, canReadLibrary: true },
  },
  hospitalaria: {
    displayName: 'Gran Hospitalaria · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canManageHospitalariaRegularity: true, canReviewCeremonies: true, canReadLibrary: true },
  },
  secretariat: {
    displayName: 'Gran Secretaría · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canManageGrandSecretariat: true, canReviewCeremonies: true, canManageDocuments: true, canReadLibrary: true },
  },
  grandMaster: {
    displayName: 'Gran Maestra · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canAuthorizeCeremonies: true, canReviewCeremonies: true, canReadLibrary: true },
  },
  systemAdmin: {
    displayName: 'Administrador del Sistema · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canConfigureSystem: true, canBootstrapInstitutional: true, canManageDocuments: true, canReadLibrary: true },
  },
  grandLodge: {
    displayName: 'Autoridad de Gran Logia · Demostración',
    accessScope: 'order',
    capabilities: {
      canApproveTransfers: true,
      canRunRegimenInteriorReports: true,
      canManageGrandSecretariat: true,
      canManageTreasuryRegularity: true,
      canManageHospitalariaRegularity: true,
      canEvaluateCeremonies: true,
      canReviewCeremonies: true,
      canValidateCeremonyInternalAffairs: true,
      canAuthorizeCeremonies: true,
      canManagePrivacy: true,
      canReadLibrary: true,
      canManageLodgeOperations: true,
      canReadLodgeSecretariat: true,
      canManageLodgeSecretariat: true,
      canManageDocuments: true,
      canManageGrandArchive: true,
      canBootstrapInstitutional: true,
      canConfigureSystem: true,
    },
  },
}

export function getDemoProfile(key: DemoProfileKey): DemoSessionProfile {
  return demoProfiles[key]
}
