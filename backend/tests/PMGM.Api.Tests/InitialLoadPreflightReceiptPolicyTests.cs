using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadPreflightReceiptPolicyTests
{
    [Fact]
    public void Missing_key_or_csv_cannot_issue_a_receipt()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(null, null, utc));
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(null, new byte[32], utc));
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(
            new Dictionary<string, string>(), new byte[31], utc));
    }

    [Fact]
    public void Untrusted_token_fails_closed()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        var result = InitialLoadPreflightReceiptPolicy.Verify(null, "invalid", new byte[32], utc);
        Assert.False(result.Valid);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    private static Dictionary<string, string> Fixture()
    {
        var files = InitialLoadTemplateEndpoints.Sheets.ToDictionary(
            s => s.Name, s => string.Join(",", s.Columns) + "\r\n",
            StringComparer.Ordinal);
        files["Talleres.csv"] += "qa23,23,Logia de prueba,STGO,Santiago,Chile\r\n";
        files["Hermanos.csv"] +=
            "p1,passport,FAKE1234,Test,Member,test@example.invalid,qa23,3,active\r\n";
        files["CargosVigentes.csv"] += "p1,qa23,worshipful_master,2026-01-01\r\n";
        return files;
    }

    [Fact]
    public void Valid_synthetic_receipt_is_verifiable_but_not_apply_permission()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        var files = Fixture();
        var key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var proof = InitialLoadPreflightReceiptPolicy.Issue(files, key, utc);
        Assert.NotNull(proof);
        Assert.False(proof.CanApply);
        Assert.Equal(0, proof.WritesPerformed);
        var result = InitialLoadPreflightReceiptPolicy.Verify(
            files, proof.Token, key, utc.AddMinutes(1));
        Assert.True(result.Valid);
        Assert.False(result.CanApply);
        Assert.Equal(proof.BatchFingerprint, result.BatchFingerprint);
        Assert.DoesNotContain("test@example.invalid", proof.Token);
        Assert.DoesNotContain("FAKE1234", proof.BatchFingerprint);
    }

    [Fact]
    public void Altered_csv_and_wrong_key_are_rejected_without_any_cell_value()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        var files = Fixture();
        var key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var proof = InitialLoadPreflightReceiptPolicy.Issue(files, key, utc)!;
        var changed = Fixture();
        changed["Hermanos.csv"] = changed["Hermanos.csv"]
            .Replace("test@example.invalid", "changed@example.invalid", StringComparison.Ordinal);
        var result = InitialLoadPreflightReceiptPolicy.Verify(
            changed, proof.Token, key, utc);
        Assert.False(result.Valid);
        Assert.Null(result.BatchFingerprint);
        Assert.DoesNotContain("changed@example.invalid", result.Code);

        var other = key.ToArray();
        other[0] ^= 1;
        Assert.False(InitialLoadPreflightReceiptPolicy.Verify(
            files, proof.Token, other, utc).Valid);
    }

    [Fact]
    public void Expired_or_future_timestamp_fails_closed()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        var files = Fixture();
        var key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var proof = InitialLoadPreflightReceiptPolicy.Issue(files, key, utc)!;
        Assert.True(InitialLoadPreflightReceiptPolicy.Verify(
            files, proof.Token, key, utc.AddMinutes(15)).Valid);
        Assert.False(InitialLoadPreflightReceiptPolicy.Verify(
            files, proof.Token, key, utc.AddMinutes(16)).Valid);
        Assert.False(InitialLoadPreflightReceiptPolicy.Verify(
            files, proof.Token, key, utc.AddSeconds(-1)).Valid);
    }

    [Fact]
    public void Header_only_and_unknown_sheets_cannot_produce_proof()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        var key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        var empty = InitialLoadTemplateEndpoints.Sheets.ToDictionary(
            x => x.Name, x => string.Join(",", x.Columns) + "\r\n",
            StringComparer.Ordinal);
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(empty, key, utc));
        var added = Fixture();
        added["Unrecognized.csv"] = "invalid";
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(added, key, utc));
    }
}
