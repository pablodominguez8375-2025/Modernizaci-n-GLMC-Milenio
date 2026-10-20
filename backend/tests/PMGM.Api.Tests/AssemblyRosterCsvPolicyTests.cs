using System.Text;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyRosterCsvPolicyTests
{
    private static AssemblyRosterCandidate Candidate(
        string last, string first, string rut,
        AssemblyEligibilityDecision? decision = null, string workshop = "Taller Ejemplo") =>
        new(Guid.NewGuid(), last, first, rut, workshop,
            decision ?? new AssemblyEligibilityDecision(
                AssemblyVoterEligibilityPolicy.Eligible, true, true, "FUENTE_TEST_INTERNA"));

    private static string Csv(byte[] bytes)
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    }

    [Fact]
    public void Exports_two_distinct_lists_and_does_not_authorize_official_publishing()
    {
        var denied = new AssemblyEligibilityDecision(
            AssemblyVoterEligibilityPolicy.AttendanceOnly, true, false,
            "CAUSA_DISCIPLINARIA_RESERVADA_JAMAS_EXPORTAR");
        var pending = new AssemblyEligibilityDecision(
            AssemblyVoterEligibilityPolicy.Pending, false, false,
            "EXPEDIENTE_INTERNO_RESERVADO");
        var preview = AssemblyRosterCsvPolicy.BuildPreview(
        [
            Candidate("Núñez", "Ana", "9-9"),
            Candidate("Álvarez", "Belén", "1-1"),
            Candidate("Nunez", "Carla", "8-8", denied),
            Candidate("Ochoa", "Luis", "7-7", pending)
        ]);
        var general = Csv(preview.GeneralAssemblyMembersCsvUtf8);
        var voters = Csv(preview.PreliminarySuffragistsCsvUtf8);

        Assert.False(preview.MayPublishAsOfficialRoster);
        Assert.False(preview.MayAuthorizeSuffrage);
        Assert.StartsWith("\"N°\";\"Apellidos y nombres\";\"RUT\";\"Taller\";\"Firma\";\"Estado de sufragio\"", general);
        Assert.StartsWith("\"N°\";\"Apellidos y nombres\";\"RUT\";\"Taller\";\"Firma\"", voters);
        Assert.Contains("\"1\";\"Álvarez, Belén\"", general);
        Assert.Contains("\"2\";\"Nunez, Carla\"", general);
        Assert.Contains("\"3\";\"Núñez, Ana\"", general);
        Assert.Contains("\"4\";\"Ochoa, Luis\"", general);
        Assert.DoesNotContain("Ochoa", voters);
        Assert.DoesNotContain("Nunez, Carla", voters);
        Assert.Contains("Núñez, Ana", voters);
        Assert.DoesNotContain("CAUSA_DISCIPLINARIA", general);
        Assert.DoesNotContain("EXPEDIENTE_INTERNO", general);
        Assert.DoesNotContain("CAUSA_DISCIPLINARIA", voters);
        Assert.Contains("\"\";\"Habilitado\"", general);
        Assert.Contains("\"\";\"Sin sufragio\"", general);
    }

    [Fact]
    public void Escapes_spreadsheet_formulas_quotes_newlines_and_separators()
    {
        var preview = AssemblyRosterCsvPolicy.BuildPreview(
        [
            Candidate("=HYPERLINK(\"x\")", "Test", "11-1",
                workshop: "Taller; \"A\"\nOTRA_FILA"),
            Candidate("+SUM(1;2)", "Plus", "22-2"),
            Candidate("-10", "Minus", "33-3"),
            Candidate("@SUM(1)", "At", "44-4"),
            Candidate("\u200B=2+2", "Invisible", "55-5")
        ]);
        var csv = Csv(preview.GeneralAssemblyMembersCsvUtf8);
        Assert.Contains("'=HYPERLINK(\"\"x\"\")", csv);
        Assert.Contains("'+SUM(1;2)", csv);
        Assert.Contains("'-10", csv);
        Assert.Contains("'@SUM(1)", csv);
        Assert.Contains("'\u200B=2+2", csv);
        Assert.Contains("Taller; \"\"A\"\" OTRA_FILA", csv);
        Assert.DoesNotContain("\nOTRA_FILA", csv);
        Assert.DoesNotContain("\rOTRA_FILA", csv);
    }

    [Fact]
    public void Fails_closed_on_invalid_duplicates_or_inconsistent_vote_status()
    {
        var p = Candidate("A", "B", "1-0");
        Assert.Throws<ArgumentException>(() =>
            AssemblyRosterCsvPolicy.BuildPreview([p, p]));
        Assert.Throws<ArgumentException>(() =>
            AssemblyRosterCsvPolicy.BuildPreview(
                [p with { Rut = "" }]));
        Assert.Throws<ArgumentException>(() =>
            AssemblyRosterCsvPolicy.BuildPreview(
                [p with { Eligibility = new AssemblyEligibilityDecision(
                    AssemblyVoterEligibilityPolicy.Pending, true, true, "INCONSISTENT") }]));
        Assert.Throws<ArgumentException>(() =>
            AssemblyRosterCsvPolicy.BuildPreview(
                [p with { Eligibility = new AssemblyEligibilityDecision(
                    AssemblyVoterEligibilityPolicy.Eligible, false, true, "INCONSISTENT") }]));
    }

    [Fact]
    public void Empty_preview_is_not_an_official_empty_padron()
    {
        var result = AssemblyRosterCsvPolicy.BuildPreview([]);
        Assert.False(result.MayPublishAsOfficialRoster);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Single(Csv(result.GeneralAssemblyMembersCsvUtf8).Split("\r\n"));
        Assert.Single(Csv(result.PreliminarySuffragistsCsvUtf8).Split("\r\n"));
    }
}
