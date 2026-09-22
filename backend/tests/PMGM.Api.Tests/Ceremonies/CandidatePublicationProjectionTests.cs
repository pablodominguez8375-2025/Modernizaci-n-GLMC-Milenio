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
    public void PublicProjection_DoesNotExposePrivateCandidateFileFields()
    {
        var propertyNames = typeof(CandidatePublicationPublicDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var forbidden = new[]
        {
            "RutOrInstitutionalId",
            "Rut",
            "BirthDate",
            "Phone",
            "Email",
            "Address",
            "Occupation",
            "CivilStatus",
            "Presenters",
            "InterviewSummary",
            "InternalObservations",
            "DocumentVersionId",
            "PhotoVersionId"
        };

        foreach (var property in forbidden)
        {
            Assert.DoesNotContain(property, propertyNames);
        }
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
            "Status",
            "PhotoUrl"
        };

        var actual = typeof(CandidatePublicationPublicDto)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(expected.SetEquals(actual));
    }
}
