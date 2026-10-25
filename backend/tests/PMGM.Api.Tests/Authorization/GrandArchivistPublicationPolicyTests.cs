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

    [Fact]
    public void Publication_end_defaults_to_twelve_months_and_must_be_future_within_five_years()
    {
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(now.AddMonths(12), GrandArchivePolicy.DefaultPublicationEnd(now));
        Assert.True(GrandArchivePolicy.IsValidPublicationEnd(now.AddMonths(12), now));
        Assert.False(GrandArchivePolicy.IsValidPublicationEnd(now.AddSeconds(-1), now));
        Assert.False(GrandArchivePolicy.IsValidPublicationEnd(now.AddYears(5).AddDays(1), now));
    }

    [Fact]
    public void Chosen_date_ends_at_the_close_of_the_chilean_day()
    {
        var end = GrandArchivePolicy.EndOfChileanDay(new DateOnly(2027, 1, 15));
        Assert.Equal(new DateTime(2027, 1, 15, 23, 59, 59), end.DateTime);
        Assert.Equal(TimeSpan.FromHours(-3), end.Offset); // verano austral
    }
}
