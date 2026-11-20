using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests;

/// <summary>Sponsor #694: one workshop per import batch, at most 30 members per batch (not a cap per workshop).</summary>
public sealed class InitialLoadBatchLimitTests
{
    private static IReadOnlyDictionary<string, string> Row(string file, params string[] f) =>
        InitialLoadTemplateEndpoints.Sheets.Single(s => s.Name == file).Columns
            .Zip(f).ToDictionary(x => x.First, x => x.Second, StringComparer.Ordinal);

    // Valid synthetic RUT bodies with computed check digit.
    private static string Rut(int body)
    {
        var sum = 0; var mul = 2;
        foreach (var c in body.ToString().Reverse()) { sum += (c - '0') * mul; mul = mul == 7 ? 2 : mul + 1; }
        var dv = 11 - sum % 11;
        return $"{body}-{(dv == 11 ? "0" : dv == 10 ? "K" : dv.ToString())}";
    }

    private static Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> Batch(int members, int workshops = 1)
    {
        var shops = Enumerable.Range(1, workshops)
            .Select(i => Row("Talleres.csv", $"t{i}", (9800 + i).ToString(), $"Taller {i}", "SYA", "Santiago", "Chile")).ToList();
        var people = Enumerable.Range(1, members)
            .Select(i => Row("Hermanos.csv", $"h{i}", "rut", Rut(10_000_000 + i), "Ana", "Pérez",
                $"qa{i}@example.invalid", "t1", "3", "active", "regular")).ToList();
        return new(StringComparer.Ordinal)
        {
            ["Talleres.csv"] = shops,
            ["Hermanos.csv"] = people,
            ["CargosVigentes.csv"] = []
        };
    }

    private static string[] Codes(Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> s) =>
        InitialLoadApplicabilityPolicy.Inspect(s).Blockers.Select(b => b.Code).ToArray();

    [Fact]
    public void Exactly_30_members_in_one_workshop_is_applicable() =>
        Assert.True(InitialLoadApplicabilityPolicy.Inspect(Batch(30)).Applicable);

    [Fact]
    public void Thirty_one_members_are_blocked_with_a_code_and_no_pii()
    {
        var r = InitialLoadApplicabilityPolicy.Inspect(Batch(31));
        Assert.False(r.Applicable);
        Assert.Contains(r.Blockers, b => b.Code == "MEMBER_BATCH_LIMIT_30");
        Assert.DoesNotContain("example.invalid", System.Text.Json.JsonSerializer.Serialize(r));
    }

    [Fact]
    public void More_than_one_workshop_is_blocked() =>
        Assert.Contains("ONE_WORKSHOP_PER_BATCH_REQUIRED", Codes(Batch(2, workshops: 2)));

    [Fact]
    public void Members_or_offices_of_another_workshop_are_blocked()
    {
        var s = Batch(2);
        ((Dictionary<string, string>)s["Hermanos.csv"][1])["workshopCode"] = "t2";
        // Preflight already rejects references to a workshop outside the batch; CROSS_WORKSHOP_BATCH is defense in depth.
        Assert.False(InitialLoadApplicabilityPolicy.Inspect(s).Applicable);

        var o = Batch(1);
        o["CargosVigentes.csv"] = [Row("CargosVigentes.csv", "h1", "t9", "worshipful_master", "2026-03-01", "2026")];
        Assert.False(InitialLoadApplicabilityPolicy.Inspect(o).Applicable);
    }

    [Fact]
    public void A_second_batch_of_30_for_the_same_workshop_is_a_separate_applicable_batch()
    {
        // The limit is per batch, not per workshop: successive batches are evaluated independently.
        Assert.True(InitialLoadApplicabilityPolicy.Inspect(Batch(30)).Applicable);
        Assert.True(InitialLoadApplicabilityPolicy.Inspect(Batch(30)).Applicable);
    }
}
