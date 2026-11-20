using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadPlanAdapterTests
{
    internal static Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> Sheets(
        string grade = "3", string identityType = "rut", string number = "11.111.111-1")
    {
        static IReadOnlyDictionary<string, string> Row(string file, params string[] f) =>
            InitialLoadTemplateEndpoints.Sheets.Single(s => s.Name == file).Columns
                .Zip(f).ToDictionary(x => x.First, x => x.Second, StringComparer.Ordinal);
        return new(StringComparer.Ordinal)
        {
            ["Talleres.csv"] = [Row("Talleres.csv", "t1", "9801", "Taller sintético", "SYA", "Santiago", "Chile")],
            ["Hermanos.csv"] = [Row("Hermanos.csv", "h1", identityType, number, "Ana", "Pérez",
                "qa@example.invalid", "t1", grade, "active", "regular")],
            ["CargosVigentes.csv"] = [Row("CargosVigentes.csv", "h1", "t1", "worshipful_master", "2026-03-01", "2026")]
        };
    }

    [Theory]
    [InlineData("1", "apprentice")]
    [InlineData("2", "fellowcraft")]
    [InlineData("3", "master")]
    public void Grades_use_only_the_closed_1_2_3_enumeration(string grade, string expected)
    {
        var r = InitialLoadPlanAdapter.Build(Sheets(grade));
        Assert.Equal("OK", r.Code);
        Assert.Equal(expected, r.Plan!.Members.Single().Grade);
        Assert.Equal("RUT", r.Plan.Members.Single().IdentityType);
        Assert.Equal("2026", r.Plan.Offices.Single().Period);
    }

    [Fact]
    public void Invalid_preflight_or_unmappable_grade_yields_no_plan()
    {
        Assert.Null(InitialLoadPlanAdapter.Build(Sheets("4")).Plan);
        Assert.Null(InitialLoadPlanAdapter.Build(Sheets(number: "11.111.111-9")).Plan);
        Assert.Null(InitialLoadPlanAdapter.Build(null).Plan);
    }

    [Fact]
    public void Passport_reaches_the_apply_policy_and_is_rejected_there_explicitly()
    {
        var plan = InitialLoadPlanAdapter.Build(Sheets(identityType: "passport", number: "AB123456")).Plan!;
        var inspected = InitialLoadMemberRowPolicy.Inspect(plan.Members);
        Assert.False(inspected.Valid);
        Assert.Contains(inspected.Findings, f => f.Code == "IDENTITY_TYPE_UNSUPPORTED");
    }
}
