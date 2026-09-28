import type { SessionProfile } from './api/pmgmApi'

export type DemoProfileKey = 'brother' | 'lodge' | 'lodgeTreasurer' | 'lodgeSecretary' | 'lodgeHospitalaria' | 'lodgeOrator' | 'lodgeFirstWarden' | 'lodgeSecondWarden' | 'lodgePastMaster' | 'grandFirstWarden' | 'grandSecondWarden' | 'immediatePastGrandMaster' | 'instructionDepartmentHead' | 'regimen' | 'treasury' | 'hospitalaria' | 'secretariat' | 'grandMaster' | 'systemAdmin' | 'grandLodge'

type ExtendedDemoCapabilities = SessionProfile['capabilities'] & {
  canBootstrapInstitutional?: boolean
  canManageLodgeOperations?: boolean
  canManageDocuments?: boolean
  canReadLibrary?: boolean
  canManageGrandArchive?: boolean
  canReadLodgeSecretariat?: boolean
  canManageLodgeSecretariat?: boolean
  canConfigureSystem?: boolean
  canManageLodgeTreasury?: boolean
  canReadLodgeHospitalaria?: boolean
  canManageLodgeHospitalaria?: boolean
  canApproveLodgeExpenses?: boolean
  canReadLodgeCouncilSummary?: boolean
  canManageLodgeCouncilSummaryAccess?: boolean
  canManageAnyWorkshopProfile?: boolean
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
      canReadLodgeCouncilSummary: false,
      canManageLodgeCouncilSummaryAccess: false,
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
      canReadLodgeHospitalaria: true,
      canApproveLodgeExpenses: true,
      canManageDocuments: true,
      canManageGrandArchive: false,
      canBootstrapInstitutional: false,
      canReadLodgeCouncilSummary: true,
      canManageLodgeCouncilSummaryAccess: true,
    },
  },
  lodgeTreasurer: {
    displayName: 'Tesorero del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: false, canManageLodgeTreasury: true, canManageDocuments: false, canReadLodgeCouncilSummary: true },
  },
  lodgeSecretary: {
    displayName: 'Secretaría del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canReadLodgeSecretariat: true, canManageLodgeSecretariat: true, canManageDocuments: true, canReadLodgeCouncilSummary: true, canManageAnyWorkshopProfile: true },
  },
  lodgeHospitalaria: {
    displayName: 'Hospitalaria del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: false, canReadLodgeHospitalaria: true, canManageLodgeHospitalaria: true, canManageDocuments: true, canReadLodgeCouncilSummary: true },
  },
  lodgeOrator: {
    displayName: 'Orador del Taller · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canReadLodgeSecretariat: true, canManageLodgeSecretariat: false, canManageDocuments: true, canReadLodgeCouncilSummary: true },
  },
  lodgeFirstWarden: {
    displayName: 'Primer Vigilante · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true, canReadLodgeCouncilSummary: true, canManageFellowcraftInstruction: true },
  },
  lodgeSecondWarden: {
    displayName: 'Segundo Vigilante · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true, canReadLodgeCouncilSummary: true, canManageApprenticeInstruction: true },
  },
  lodgePastMaster: {
    displayName: 'Ex Venerable Maestro · Demostración', accessScope: 'organization',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canManageLodgeOperations: true, canManageDocuments: true, canReadLodgeCouncilSummary: true, canManageMasterInstruction: true },
  },
  grandFirstWarden: {
    displayName: 'Gran Primer Vigilante · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canReadOrderFellowcraftInstructions: true },
  },
  grandSecondWarden: {
    displayName: 'Gran Segundo Vigilante · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canReadOrderApprenticeInstructions: true },
  },
  immediatePastGrandMaster: {
    displayName: 'Inmediato Ex Gran Maestro · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canReadOrderMasterInstructions: true },
  },
  instructionDepartmentHead: {
    displayName: 'Jefatura de Docencia · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canReadLibrary: true, canReadAllOrderInstructions: true, canReadOrderApprenticeInstructions: true, canReadOrderFellowcraftInstructions: true, canReadOrderMasterInstructions: true },
  },
  regimen: {
    displayName: 'Régimen Interior · Demostración', accessScope: 'order',
    capabilities: { ...deniedCoreCapabilities, canRunRegimenInteriorReports: true, canReviewCeremonies: true, canValidateCeremonyInternalAffairs: true, canReadLibrary: true, canManageAnyWorkshopProfile: true },
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
    capabilities: { ...deniedCoreCapabilities, canManageGrandSecretariat: true, canReviewCeremonies: true, canManageDocuments: true, canReadLibrary: true, canManageAnyWorkshopProfile: true },
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
