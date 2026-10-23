using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.GrandArchive;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class GrandArchivistPublicationPolicyTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Only_the_grand_archivist_profile_can_publish()
    {
        Assert.True(GrandArchivePolicy.IsGrandArchivist(User((InstitutionalClaims.Role, InstitutionalRoles.GrandArchivist), (InstitutionalClaims.Scope, "order"))));
        Assert.False(GrandArchivePolicy.IsGrandArchivist(User((InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin), (InstitutionalClaims.Scope, "order"))));
        Assert.False(GrandArchivePolicy.IsGrandArchivist(User((InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin), (InstitutionalClaims.Scope, "order"))));
        Assert.False(GrandArchivePolicy.IsGrandArchivist(User((InstitutionalClaims.Role, InstitutionalRoles.GrandArchivist), (InstitutionalClaims.Scope, "organization"))));
        Assert.False(GrandArchivePolicy.IsGrandArchivist(User((InstitutionalClaims.Role, InstitutionalRoles.GrandArchivist))));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(99, false)]
    public void Degree_is_one_two_or_three(int degree, bool valid)
        => Assert.Equal(valid, GrandArchivePolicy.IsValidDegree(degree));

    [Theory]
    [InlineData("decree", true)]
    [InlineData("communication", true)]
    [InlineData("minute", true)]
    [InlineData("resolution", true)]
    [InlineData("regulation", true)]
    [InlineData("other", true)]
    [InlineData("correspondence", false)]
    [InlineData("historical_record", false)]
    public void Internal_record_types_are_not_publishable(string type, bool publishable)
        => Assert.Equal(publishable, GrandArchiveCodes.RecordType.IsPublishable(type));
}
