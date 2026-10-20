using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyReviewDecisionAdmissionPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 11, 0, 0, TimeSpan.Zero);

    private static ClaimsPrincipal Actor(string role = InstitutionalRoles.RegimenInterior,
        bool scope = true, bool authenticated = true)
    {
        var claims = new List<Claim>
        {
            new("sub", "ri-sintetico"), new(InstitutionalClaims.Role, role)
        };
        if (scope) claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims,
            authenticated ? "SyntheticAuthentication" : null));
    }

    private static AssemblyRecord Assembly() => new()
    {
        Name = "Gran Asamblea sintética",
        MeetingDate = new DateOnly(2026, 12, 30),
        CutoffDate = new DateOnly(2026, 10, 19),
        CreatedBySubject = "ri-sintetico"
    };

    private static AssemblyMemberStatusRecord Member(Guid assemblyId) => new()
    {
        AssemblyId = assemblyId,
        MemberId = Guid.NewGuid(),
        PersonId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Category = "representative",
        SourceOfficeAssignmentId = Guid.NewGuid(),
        SourceEvidenceReference = "cargo-ejemplo",
        LastNamesAtCutoff = "Apellido ficticio",
        FirstNamesAtCutoff = "Nombre ficticio",
        RutAtCutoff = "SIN-DATOS-REALES",
        WorkshopAtCutoff = "Taller ficticio"
    };

    [Theory]
    [InlineData(AssemblyReviewDecisionAdmissionPolicy.RequestCorrection)]
    [InlineData(AssemblyReviewDecisionAdmissionPolicy.ReferValidation)]
    [InlineData(AssemblyReviewDecisionAdmissionPolicy.ConfirmIneligible)]
    [InlineData(AssemblyReviewDecisionAdmissionPolicy.AttendanceOnly)]
    public void Approved_preliminary_actions_create_append_only_intents(string action)
    {
        var assembly = Assembly();
        var evt = AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(), assembly, Member(assembly.Id), action,
            "Fundamento de revisión sintética", "Plancha RI ficticia", Now);
        Assert.NotNull(evt);
        Assert.Equal(action, evt.Action);
        Assert.Equal(assembly.Id, evt.AssemblyId);
        Assert.Equal("ri-sintetico", evt.DecidedBySubject);
    }

    [Theory]
    [InlineData(InstitutionalRoles.PlatformSuperAdmin)]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    [InlineData(InstitutionalRoles.GranSecretaria)]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    public void Only_explicit_authenticated_regimen_role_may_prepare_event(string role)
    {
        var assembly = Assembly();
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(role), assembly, Member(assembly.Id), "request_correction",
            "Con fundamento", "Referencia", Now));
    }

    [Fact]
    public void Rejects_unauthenticated_missing_scope_foreign_member_and_closed_assembly()
    {
        var assembly = Assembly();
        var member = Member(assembly.Id);
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(authenticated: false), assembly, member,
            "request_correction", "Fundamento", "Referencia", Now));
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(scope: false), assembly, member,
            "request_correction", "Fundamento", "Referencia", Now));
        member.AssemblyId = Guid.NewGuid();
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(), assembly, member,
            "request_correction", "Fundamento", "Referencia", Now));
        member.AssemblyId = assembly.Id;
        assembly.Status = "closed";
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(), assembly, member,
            "request_correction", "Fundamento", "Referencia", Now));
    }

    [Fact]
    public void No_action_can_confirm_suffrage_and_every_event_needs_provenance()
    {
        var assembly = Assembly();
        var member = Member(assembly.Id);
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(), assembly, member, "confirm_eligible",
            "Fundamento", "Referencia", Now));
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(), assembly, member, "attendance_only",
            "", "Referencia", Now));
        Assert.Null(AssemblyReviewDecisionAdmissionPolicy.Prepare(
            Actor(), assembly, member, "confirm_ineligible",
            "Fundamento", "", Now));
    }
}
