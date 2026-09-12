namespace PMGM.Api.Modules.RegimenInterior;

public static class AssemblyMemberCategoryPolicy
{
    public const string Representative = "representative";
    public const string PermanentAssemblyMember = "permanent_assembly_member";

    public static AssemblyMemberCategoryDecision Classify(
        IEnumerable<WorshipfulMasterTerm> terms,
        DateOnly asOf)
    {
        var effectiveTerms = terms
            .Where(x => x.StartDate <= asOf)
            .OrderBy(x => x.StartDate)
            .ToArray();

        var completedTerms = effectiveTerms.Count(x => x.EndDate is not null && x.EndDate.Value <= asOf);
        var currentTerm = effectiveTerms.LastOrDefault(x =>
            x.StartDate <= asOf && (x.EndDate is null || x.EndDate.Value > asOf));

        if (completedTerms > 0)
        {
            return new AssemblyMemberCategoryDecision(
                PermanentAssemblyMember,
                true,
                completedTerms,
                currentTerm is not null,
                "Cumplió al menos un período como Venerable Maestro; la calidad es permanente aunque vuelva a ejercer el cargo.");
        }

        if (currentTerm is not null)
        {
            return new AssemblyMemberCategoryDecision(
                Representative,
                true,
                0,
                true,
                "Ejerce por primera vez como Venerable Maestro y representa a su Taller durante ese primer período.");
        }

        return new AssemblyMemberCategoryDecision(
            null,
            false,
            0,
            false,
            "No registra un período completo ni un primer período vigente como Venerable Maestro.");
    }
}

public sealed record WorshipfulMasterTerm(DateOnly StartDate, DateOnly? EndDate);

public sealed record AssemblyMemberCategoryDecision(
    string? Category,
    bool Qualifies,
    int CompletedTerms,
    bool CurrentlyServing,
    string Reason);
