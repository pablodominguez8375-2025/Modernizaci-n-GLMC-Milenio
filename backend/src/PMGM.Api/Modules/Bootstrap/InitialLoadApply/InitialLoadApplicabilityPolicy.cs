namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 — ONE eligibility rule shared by the preview and the apply endpoint. A batch can be
/// structurally valid (readable preview) and still NOT applicable. Codes and positions only;
/// submitted values are never returned. Database-dependent checks (exact OfficeDefinition,
/// composite workshop identity, existing people) happen inside the apply transaction.
/// </summary>
public static class InitialLoadApplicabilityPolicy
{
    public sealed record Finding(string Sheet, int Row, string Field, string Code);

    public sealed record Result(bool Applicable, IReadOnlyList<Finding> Blockers);

    public static Result Inspect(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>? sheets)
    {
        var blockers = new List<Finding>();
        if (sheets is null) return new Result(false, [new Finding("schema", 0, "files", "SHEETS_REQUIRED")]);
        if (!InitialLoadPreflightPolicy.Inspect(sheets).Valid)
            return new Result(false, [new Finding("schema", 0, "files", "PREFLIGHT_INVALID")]);

        static string V(IReadOnlyDictionary<string, string> row, string key) =>
            row.TryGetValue(key, out var v) ? (v ?? "").Trim() : "";

        var workshops = sheets["Talleres.csv"];
        for (var i = 0; i < workshops.Count; i++)
            if (V(workshops[i], "orienteCode").Length == 0)
                blockers.Add(new Finding("Talleres.csv", i + 2, "orienteCode", "ORIENTE_REQUIRED_FOR_APPLY"));

        // Sponsor #694: one workshop per import, with at most 30 members per batch.
        // This is NOT a cap on the total number of members registered in a workshop.
        if (workshops.Count != 1)
            blockers.Add(new Finding("Talleres.csv", 0, "workshopCode", "ONE_WORKSHOP_PER_BATCH_REQUIRED"));

        var members = sheets["Hermanos.csv"];
        if (members.Count > 30)
            blockers.Add(new Finding("Hermanos.csv", 0, "rows", "MEMBER_BATCH_LIMIT_30"));

        if (workshops.Count == 1)
        {
            var workshopCode = V(workshops[0], "workshopCode");
            for (var i = 0; i < members.Count; i++)
                if (!string.Equals(V(members[i], "workshopCode"), workshopCode, StringComparison.Ordinal))
                    blockers.Add(new Finding("Hermanos.csv", i + 2, "workshopCode", "CROSS_WORKSHOP_BATCH"));

            var offices = sheets["CargosVigentes.csv"];
            for (var i = 0; i < offices.Count; i++)
                if (!string.Equals(V(offices[i], "workshopCode"), workshopCode, StringComparison.Ordinal))
                    blockers.Add(new Finding("CargosVigentes.csv", i + 2, "workshopCode", "CROSS_WORKSHOP_BATCH"));
        }
        for (var i = 0; i < members.Count; i++)
        {
            if (!InitialLoadMemberRowPolicy.AdmissibleInstitutionalStatuses.Contains(V(members[i], "institutionalStatus")))
                blockers.Add(new Finding("Hermanos.csv", i + 2, "institutionalStatus", "STATUS_NOT_APPLICABLE"));
            if (V(members[i], "identityType") != "rut")
                blockers.Add(new Finding("Hermanos.csv", i + 2, "identityType", "IDENTITY_TYPE_UNSUPPORTED"));
        }

        // Anything the adapter cannot map (e.g. a grade outside the closed 1/2/3 list) blocks too.
        if (blockers.Count == 0 && InitialLoadPlanAdapter.Build(sheets).Plan is null)
            blockers.Add(new Finding("schema", 0, "files", "PLAN_NOT_BUILDABLE"));

        return new Result(blockers.Count == 0, blockers);
    }
}
