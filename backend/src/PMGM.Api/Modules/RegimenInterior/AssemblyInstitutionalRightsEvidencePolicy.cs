namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Efectos electorales de fuentes oficiales; nunca consulta expedientes
/// disciplinarios completos ni infiere inexistencia de restricciones.
/// </summary>
public static class AssemblyInstitutionalRightsEvidencePolicy
{
    public static AssemblyInstitutionalRightsResult Evaluate(
        Guid personId, Guid workshopId, DateOnly cutoff,
        AssemblyMembershipRightsEvidence? membership,
        AssemblyRightsSourceCoverage? honor,
        AssemblyRightsSourceCoverage? regimen,
        IReadOnlyCollection<AssemblyRightsRestrictionEvidence> restrictions)
    {
        ArgumentNullException.ThrowIfNull(restrictions);
        if (personId == Guid.Empty || workshopId == Guid.Empty)
            return AssemblyInstitutionalRightsResult.Unknown;

        bool memberVerified = membership is not null &&
            membership.PersonId == personId && membership.WorkshopId == workshopId &&
            membership.AsOfDate == cutoff && membership.Verified &&
            !string.IsNullOrWhiteSpace(membership.SourceReference);

        var honorValid = ValidCoverage(honor, personId, cutoff, "tribunal_honor");
        var regimenValid = ValidCoverage(regimen, personId, cutoff, "regimen_interior");
        // No concluir ausencia de sanción si algún registro es contradictorio
        // o carece de procedencia. Se requiere cobertura completa por fuente.
        bool restrictionsValid = restrictions.All(x =>
            x.PersonId == personId &&
            (x.SourceAuthority == "tribunal_honor" || x.SourceAuthority == "regimen_interior") &&
            x.Verified && !string.IsNullOrWhiteSpace(x.SourceReference) &&
            (x.EndsOn is null || x.EndsOn >= x.StartsOn));

        if (!memberVerified || !honorValid || !regimenValid || !restrictionsValid)
            return AssemblyInstitutionalRightsResult.Unknown;

        var active = restrictions.Where(x => x.StartsOn <= cutoff &&
            (x.EndsOn is null || x.EndsOn >= cutoff) &&
            (x.RevokedOn is null || x.RevokedOn > cutoff)).ToArray();

        bool memberActive = membership!.MemberActive;
        bool institutionalRights = membership.InstitutionalRightsActive &&
            !active.Any(x => x.AffectsInstitutionalRights);

        return new AssemblyInstitutionalRightsResult(
            memberActive, institutionalRights,
            !active.Any(x => x.SourceAuthority == "tribunal_honor" && x.AffectsAttendance),
            !active.Any(x => x.SourceAuthority == "tribunal_honor" && x.AffectsSuffrage),
            !active.Any(x => x.SourceAuthority == "regimen_interior" && x.AffectsAttendance),
            !active.Any(x => x.SourceAuthority == "regimen_interior" && x.AffectsSuffrage));
    }

    private static bool ValidCoverage(AssemblyRightsSourceCoverage? coverage,
        Guid personId, DateOnly cutoff, string authority) =>
        coverage is not null && coverage.PersonId == personId &&
        coverage.Cutoff == cutoff && coverage.SourceAuthority == authority &&
        coverage.Complete && coverage.Verified &&
        !string.IsNullOrWhiteSpace(coverage.SourceReference);
}

public sealed record AssemblyMembershipRightsEvidence(
    Guid PersonId, Guid WorkshopId, DateOnly AsOfDate,
    bool MemberActive, bool InstitutionalRightsActive,
    bool Verified, string SourceReference);

public sealed record AssemblyRightsSourceCoverage(
    Guid PersonId, DateOnly Cutoff, string SourceAuthority,
    bool Complete, bool Verified, string SourceReference);

public sealed record AssemblyRightsRestrictionEvidence(
    Guid PersonId, string SourceAuthority, DateOnly StartsOn, DateOnly? EndsOn,
    DateOnly? RevokedOn, bool AffectsAttendance, bool AffectsSuffrage,
    bool AffectsInstitutionalRights, bool Verified, string SourceReference);

public sealed record AssemblyInstitutionalRightsResult(
    bool? MemberActive, bool? InstitutionalRightsActive,
    bool? HonorAllowsAttendance, bool? HonorAllowsSuffrage,
    bool? RegimenAllowsAttendance, bool? RegimenAllowsSuffrage)
{
    public static readonly AssemblyInstitutionalRightsResult Unknown =
        new(null, null, null, null, null, null);
}
