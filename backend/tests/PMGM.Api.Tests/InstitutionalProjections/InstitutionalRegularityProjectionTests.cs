using PMGM.Api.Modules.InstitutionalProjections;
using Xunit;

namespace PMGM.Api.Tests.InstitutionalProjections;

public sealed class InstitutionalRegularityProjectionTests
{
    [Fact]
    public void CeremonyProjection_ContainsOnlyPurposeBoundFields()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AsOfDate",
            "Treasury",
            "Hospitalaria",
            "WorkshopComplies"
        };

        var actual = typeof(CeremonyRegularityProjectionDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(expected.SetEquals(actual));
    }

    [Fact]
    public void RegularitySignal_DoesNotExposeAdministrativeDetailsOrIdentifiers()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Status",
            "SourceAsOfDate",
            "Complies"
        };

        var actual = typeof(InstitutionalRegularitySignalDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(expected.SetEquals(actual));
        Assert.DoesNotContain("Id", actual);
        Assert.DoesNotContain("OrganizationId", actual);
        Assert.DoesNotContain("MemberId", actual);
        Assert.DoesNotContain("SourceReference", actual);
        Assert.DoesNotContain("Notes", actual);
    }
}
