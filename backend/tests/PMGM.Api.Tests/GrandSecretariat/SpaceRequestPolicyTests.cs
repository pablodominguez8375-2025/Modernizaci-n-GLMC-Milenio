using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.GrandSecretariat;
using Xunit;

namespace PMGM.Api.Tests.GrandSecretariat;

public sealed class SpaceRequestPolicyTests
{
    private static readonly Guid LodgeA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid LodgeB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly InstitutionalAccessService Access = new();

    private static ClaimsPrincipal Actor(string role, params Guid[] orgs)
    {
        var claims = new List<Claim> { new(InstitutionalClaims.Role, role) };
        claims.AddRange(orgs.Select(x => new Claim(InstitutionalClaims.Organization, x.ToString())));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public void Secretary_is_resolved_only_from_her_own_unambiguous_lodge()
    {
        Assert.True(SpaceRequestEndpoints.TryLodge(Actor(InstitutionalRoles.TallerSecretaria, LodgeA), Access, out var id));
        Assert.Equal(LodgeA, id);
        Assert.False(SpaceRequestEndpoints.TryLodge(Actor(InstitutionalRoles.TallerSecretaria), Access, out _));
        Assert.False(SpaceRequestEndpoints.TryLodge(Actor(InstitutionalRoles.TallerSecretaria, LodgeA, LodgeB), Access, out _));
    }

    [Theory]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    [InlineData(InstitutionalRoles.TallerVenerable)]
    [InlineData(InstitutionalRoles.GranSecretaria)]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    public void Non_secretariat_roles_cannot_submit_lodge_space_requests(string role)
    {
        Assert.False(SpaceRequestEndpoints.TryLodge(Actor(role, LodgeA), Access, out _));
    }

    private static CreateWorkshopSpaceRequest Valid(DateTimeOffset? start = null)
    {
        var from = start ?? new DateTimeOffset(2035, 10, 20, 10, 0, 0, TimeSpan.Zero);
        return new CreateWorkshopSpaceRequest("temple", null, from, from.AddHours(2),
            "tenida", null, "Sesión de prueba", 30, null, null, null);
    }

    [Fact]
    public void Future_utc_request_is_valid()
    {
        Assert.Null(SpaceRequestEndpoints.Validate(Valid()));
    }

    [Fact]
    public void Rejects_inverted_dates_unknown_categories_and_misaligned_ceremony()
    {
        var source = Valid();
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with { EndsAtUtc = source.StartsAtUtc }));
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with { SpaceType = "unknown" }));
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with { Purpose = "undefined" }));
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with {
            CeremonyRequestId = Guid.NewGuid(), Purpose = "tenida" }));
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with { Notes = new string('x', 501) }));
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with { ExpectedAttendees = 0 }));
    }

    [Fact]
    public void Requires_utc_offsets_and_future_start()
    {
        var source = Valid();
        var nonUtc = new DateTimeOffset(2035, 10, 20, 7, 0, 0, TimeSpan.FromHours(-3));
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with { StartsAtUtc = nonUtc }));
        Assert.NotNull(SpaceRequestEndpoints.Validate(source with { StartsAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }));
    }
}
