namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Acreditación de la calidad de asambleísta. OfficeAssignment.EndDate no
/// demuestra por sí sola cumplimiento íntegro del período estatutario.
/// </summary>
public static class AssemblyAccreditedCategoryPolicy
{
    public static AssemblyMemberCategoryDecision Classify(
        IEnumerable<AssemblyMasterOfficeEvidence> terms, DateOnly cutoff)
    {
        var verified = terms
            .Where(t => t.OfficeAssignmentId != Guid.Empty &&
                        t.StartDate <= cutoff &&
                        t.InstitutionallyVerified &&
                        !string.IsNullOrWhiteSpace(t.EvidenceReference) &&
                        (t.EndDate is null || t.EndDate.Value >= t.StartDate))
            .ToArray();

        var completed = verified.Count(t =>
            t.FullTermCompleted &&
            t.EndDate is not null &&
            t.EndDate.Value <= cutoff);

        var current = verified.Any(t =>
            t.CurrentServiceVerified &&
            t.StartDate <= cutoff &&
            (t.EndDate is null || t.EndDate.Value >= cutoff));

        if (completed > 0)
            return new AssemblyMemberCategoryDecision(
                AssemblyMemberCategoryPolicy.PermanentAssemblyMember,
                true, completed, current,
                "Cuenta con un período íntegramente cumplido y acreditado como Venerable Maestro.");

        if (current)
            return new AssemblyMemberCategoryDecision(
                AssemblyMemberCategoryPolicy.Representative,
                true, 0, true,
                "Primer período de Venerable Maestro activo y acreditado; sin período completo previo verificado.");

        return new AssemblyMemberCategoryDecision(
            null, false, 0, false,
            "No consta primer período vigente ni un período íntegramente cumplido con acreditación institucional.");
    }
}

public sealed record AssemblyMasterOfficeEvidence(
    Guid OfficeAssignmentId,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool InstitutionallyVerified,
    string? EvidenceReference,
    bool FullTermCompleted,
    bool CurrentServiceVerified);
