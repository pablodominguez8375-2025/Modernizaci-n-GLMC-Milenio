namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Une evidencia institucional validada con el motor electoral existente.
/// No obtiene evidencias de clientes HTTP ni considera un estado desconocido
/// como permiso. Fuentes y saldos se deben resolver con adaptadores backend.
/// </summary>
public static class AssemblyEvidencedEligibilityPolicy
{
    public static AssemblyEligibilityDecision Evaluate(
        Guid workshopId, DateOnly cutoff,
        AssemblyMemberCategoryDecision accreditedCategory,
        AssemblyInstitutionalRightsResult rights,
        AssemblyGrandTreasuryBalance? verifiedBalance,
        AssemblyDebtException? authorizedDebtException)
    {
        ArgumentNullException.ThrowIfNull(accreditedCategory);
        ArgumentNullException.ThrowIfNull(rights);

        var isAssemblyMember = accreditedCategory.Qualifies &&
            (accreditedCategory.Category == AssemblyMemberCategoryPolicy.Representative ||
             accreditedCategory.Category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember);
        return AssemblyVoterEligibilityPolicy.Evaluate(new AssemblyEligibilityFacts(
            workshopId, cutoff, isAssemblyMember,
            rights.MemberActive, rights.InstitutionalRightsActive,
            rights.HonorAllowsAttendance, rights.RegimenAllowsAttendance,
            rights.HonorAllowsSuffrage, rights.RegimenAllowsSuffrage,
            verifiedBalance, authorizedDebtException));
    }
}
