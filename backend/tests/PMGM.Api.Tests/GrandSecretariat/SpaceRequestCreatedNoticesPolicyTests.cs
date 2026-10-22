using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.GrandSecretariat;
using Xunit;

namespace PMGM.Api.Tests.GrandSecretariat;

public sealed class SpaceRequestCreatedNoticesPolicyTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private static DynamicAccessCatalog Catalog(
        bool activated = true, bool order = true, bool grantView = true,
        DateOnly? expires = null, string profile = "grand-secretariat") =>
        new(1, ["view", "search"],
            [new DynamicMenu("order", "Gran Logia", true,
                [new DynamicView("secretariat", "Gran Secretaría", true)])],
            [new DynamicProfile(Guid.NewGuid(), profile, "Gran Secretaría",
                order ? "order" : "lodge", false, true, ["order"],
                [new DynamicGrant("secretariat",
                    grantView ? ["view", "search"] : ["search"])])],
            [new DynamicAssignment(Guid.NewGuid(), "subject-order-secretary",
                profile, order ? null : Guid.NewGuid(), Today.AddDays(-1),
                expires, activated)]);

    [Fact]
    public void Requires_explicit_grand_secretariat_profile_and_order_scope()
    {
        Assert.Empty(SpaceRequestCreatedNotices.SelectAssignedSubjects(
            DynamicAccessEndpoints.CreateCatalog(), Today));
        Assert.Empty(SpaceRequestCreatedNotices.SelectAssignedSubjects(
            Catalog(profile: "grand-lodge-admin"), Today));
        Assert.Empty(SpaceRequestCreatedNotices.SelectAssignedSubjects(
            Catalog(order: false), Today));
        Assert.Empty(SpaceRequestCreatedNotices.SelectAssignedSubjects(
            Catalog(grantView: false), Today));
        Assert.Empty(SpaceRequestCreatedNotices.SelectAssignedSubjects(
            Catalog(activated: false), Today));
        Assert.Empty(SpaceRequestCreatedNotices.SelectAssignedSubjects(
            Catalog(expires: Today.AddDays(-1)), Today));
    }

    [Fact]
    public void Active_explicit_order_level_assignment_is_candidate_not_delivery_authorization()
    {
        var subjects = SpaceRequestCreatedNotices.SelectAssignedSubjects(Catalog(), Today);
        Assert.Equal(["subject-order-secretary"], subjects);
        // El verificador de member_identity_links y membresía efectiva corre
        // por separado dentro de la transacción antes de entregar avisos.
    }
}
