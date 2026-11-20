namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 — turns the three canonical sheets (already normalised by the preview contract) into
/// the typed inputs of the orchestrator. Pure. Fail-closed: the static preflight must pass first
/// and the numeric grade is mapped ONLY through the closed 1/2/3 enumeration of the contract
/// (1=apprentice, 2=fellowcraft, 3=master); any other value yields no plan. Identity types other
/// than "rut" are passed through as-is so the apply policy rejects them explicitly.
/// </summary>
public static class InitialLoadPlanAdapter
{
    public sealed record Plan(
        IReadOnlyList<InitialLoadWorkshopMatchPolicy.Incoming> Workshops,
        IReadOnlyList<InitialLoadMemberRowPolicy.Row> Members,
        IReadOnlyList<InitialLoadOfficeRowPolicy.Row> Offices);

    public sealed record Result(Plan? Plan, string Code);

    private static readonly IReadOnlyDictionary<string, string> Grades =
        new Dictionary<string, string>(StringComparer.Ordinal)
        { ["1"] = "apprentice", ["2"] = "fellowcraft", ["3"] = "master" };

    public static Result Build(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? sheets)
    {
        if (sheets is null) return new Result(null, "SHEETS_REQUIRED");
        var preflight = InitialLoadPreflightPolicy.Inspect(sheets);
        if (!preflight.Valid) return new Result(null, "PREFLIGHT_INVALID");

        static string V(IReadOnlyDictionary<string, string> row, string key) =>
            row.TryGetValue(key, out var v) ? (v ?? "").Trim() : "";

        var workshops = sheets["Talleres.csv"].Select(r => new InitialLoadWorkshopMatchPolicy.Incoming(
            V(r, "workshopCode"), V(r, "number"), V(r, "name"), V(r, "orienteCode"),
            V(r, "city"), V(r, "country"))).ToArray();

        var members = new List<InitialLoadMemberRowPolicy.Row>();
        foreach (var r in sheets["Hermanos.csv"])
        {
            if (!Grades.TryGetValue(V(r, "grade"), out var grade)) return new Result(null, "GRADE_NOT_MAPPABLE");
            var type = V(r, "identityType");
            members.Add(new InitialLoadMemberRowPolicy.Row(
                V(r, "personRef"), type == "rut" ? "RUT" : type.ToUpperInvariant(), V(r, "identityNumber"),
                V(r, "firstNames"), V(r, "lastNames"), V(r, "email"), V(r, "workshopCode"),
                grade, V(r, "institutionalStatus"), V(r, "membershipType")));
        }

        var offices = sheets["CargosVigentes.csv"].Select(r => new InitialLoadOfficeRowPolicy.Row(
            V(r, "personRef"), V(r, "workshopCode"), V(r, "officeCode"), V(r, "startDate"), V(r, "period"))).ToArray();

        return new Result(new Plan(workshops, members, offices), "OK");
    }
}
