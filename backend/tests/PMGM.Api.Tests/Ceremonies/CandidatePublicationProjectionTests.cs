using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class CandidatePublicationProjectionTests
{
    [Fact]
    public void PublicProjection_DoesNotExposeInternalIdentifiers()
    {
        var propertyNames = typeof(CandidatePublicationPublicDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Id", propertyNames);
        Assert.DoesNotContain("PersonId", propertyNames);
        Assert.DoesNotContain("CeremonyRequestId", propertyNames);
        Assert.DoesNotContain("OrganizationId", propertyNames);
    }

    [Fact]
    public void PublicProjection_ContainsOnlyPurposeBoundFields()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DisplayName",
            "WorkshopName",
            "WorkshopNumber",
            "PublishedFromUtc",
            "PublishedUntilUtc",
            "RequiredDays",
            "ElapsedDays",
            "ComplianceDateUtc",
            "RuleCode",
            "Status"
        };

        var actual = typeof(CandidatePublicationPublicDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(expected.SetEquals(actual));
    }
}
