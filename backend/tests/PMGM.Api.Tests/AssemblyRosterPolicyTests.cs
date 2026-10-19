using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyRosterPolicyTests
{
    private static AssemblyEligibilityDecision Eligible =>
        new(AssemblyVoterEligibilityPolicy.Eligible, true, true, "HABILITACION_VERIFICADA");

    private static AssemblyEligibilityDecision Denied =>
        new(AssemblyVoterEligibilityPolicy.AttendanceOnly, true, false,
            "CAUSA_INTERNA_NO_EXPORTABLE");

    private static AssemblyRosterCandidate Person(
        string surname, string given, string rut, AssemblyEligibilityDecision? decision = null) =>
        new(Guid.NewGuid(), surname, given, rut, "Taller Ficticio", decision ?? Eligible);

    [Fact]
    public void Sorts_lastnames_and_given_names_es_cl_without_accents_but_keeps_enye_distinct()
    {
        var rows = AssemblyRosterPolicy.Asambleistas(
        [
            Person("Ochoa", "Zoe", "8-7"),
            Person("Núñez", "Ana", "6-5"),
            Person("Álvarez", "Elena", "2-1"),
            Person("Nunez", "Ana", "4-3"),
            Person("Alvarez", "Pedro", "3-2"),
            Person("Álvarez", "Elena", "1-0"),
            Person("García de la Fuente", "Camila", "5-4"),
        ]);
        Assert.Equal(new[]
        {
            "Álvarez, Elena", "Álvarez, Elena", "Alvarez, Pedro",
            "García de la Fuente, Camila", "Nunez, Ana", "Núñez, Ana",
            "Ochoa, Zoe"
        }, rows.Select(r => r.LastNamesAndFirstNames).ToArray());
        Assert.Equal(new[] { "1-0", "2-1" }, rows.Take(2).Select(x => x.Rut));
        Assert.Equal(Enumerable.Range(1, 7), rows.Select(x => x.Number));
        Assert.All(rows, x => Assert.Equal("", x.Signature));
    }

    [Fact]
    public void Full_list_contains_non_voters_with_no_disclosed_discliplinary_causes()
    {
        var rows = AssemblyRosterPolicy.Asambleistas(
        [
            Person("Bravo", "B", "2-1", Denied),
            Person("Alvarez", "A", "1-0", Eligible)
        ]);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Sin sufragio", rows.Single(x => x.Rut == "2-1").SuffrageMark);
        Assert.DoesNotContain(rows, x => x.LastNamesAndFirstNames.Contains("CAUSA_INTERNA"));
    }

    [Fact]
    public void Padron_only_includes_voting_eligible_people_with_empty_signature()
    {
        var inconsistent = Eligible with { MayVote = false };
        var rows = AssemblyRosterPolicy.PadronSufragantes(
        [
            Person("Bustos", "Ben", "2-2", Denied),
            Person("Núñez", "Ana", "4-4", new(
                AssemblyVoterEligibilityPolicy.Pending, false, false, "SIN_SNAPSHOT")),
            Person("Alvarez", "Ana", "1-1"),
            Person("Diaz", "David", "3-3", inconsistent)
        ]);
        Assert.Single(rows);
        Assert.Equal("Alvarez, Ana", rows[0].LastNamesAndFirstNames);
        Assert.Equal(1, rows[0].Number);
        Assert.Null(rows[0].SuffrageMark);
        Assert.Equal("", rows[0].Signature);
    }

    [Fact]
    public void Normalization_drops_acute_accents_but_not_the_enye()
    {
        Assert.Equal("Alvarez", AssemblyRosterPolicy.NormalizeForSpanishCollation("Álvarez"));
        Assert.Equal("Niño", AssemblyRosterPolicy.NormalizeForSpanishCollation("Niño"));
        Assert.Equal("Nino", AssemblyRosterPolicy.NormalizeForSpanishCollation("Ninó"));
    }
}
