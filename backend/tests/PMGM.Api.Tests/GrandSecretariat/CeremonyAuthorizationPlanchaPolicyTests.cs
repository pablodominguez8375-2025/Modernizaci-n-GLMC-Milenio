using PMGM.Api.Modules.GrandSecretariat;
using Xunit;

namespace PMGM.Api.Tests.GrandSecretariat;

public sealed class CeremonyAuthorizationPlanchaPolicyTests
{
    [Fact]
    public void UsesAnnualSequenceWithoutInterpretingLegacyRandomCodesAsNumbers()
    {
        var historical = new[] {
            "PLA-AUT-CER-2026-AB12CD34",
            "PLA-AUT-CER-2026-000004",
            "PLA-AUT-CER-2025-999999",
            "PLA-COM-2026-000005",
            "PLA-AUT-CER-2026-000002"
        };
        Assert.Equal("PLA-AUT-CER-2026-000005",
            CeremonyAuthorizationPlanchaPolicy.NextCode(2026, historical));
        Assert.Equal("PLA-AUT-CER-2027-000001",
            CeremonyAuthorizationPlanchaPolicy.NextCode(2027, historical));
    }

    [Fact]
    public void SequenceDoesNotOverflowSilently()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CeremonyAuthorizationPlanchaPolicy.NextCode(2026,
                new[] { "PLA-AUT-CER-2026-999999" }));
    }

    [Fact]
    public void IssuedPlanchaContainsOfficialNumberAndFrozenAuditReference()
    {
        var id = Guid.NewGuid();
        var auditId = Guid.NewGuid();
        var frozenMetadata = "{\"regularity\":\"approved\"}";
        var digest = CeremonyAuthorizationPlanchaPolicy.SnapshotDigest(frozenMetadata);
        var text = CeremonyAuthorizationPlanchaPolicy.Render(
            "PLA-AUT-CER-2026-000001", id, "Taller de ensayo", "initiation",
            "21-10-2026", "en templo reservado", auditId, digest);
        Assert.Contains("Nº PLA-AUT-CER-2026-000001", text);
        Assert.Contains(id.ToString("D"), text);
        Assert.Contains(auditId.ToString("D"), text);
        Assert.Contains(digest, text);
        Assert.Equal(64, digest.Length);
        Assert.NotEqual(digest, CeremonyAuthorizationPlanchaPolicy.SnapshotDigest("{\"regularity\":\"rejected\"}"));
    }
}
