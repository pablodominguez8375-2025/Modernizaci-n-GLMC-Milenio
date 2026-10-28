using Xunit;
using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Tests.Authorization;

public sealed class AccountLifecyclePolicyTests
{
    private static readonly string Subject = Guid.Parse("11111111-1111-4111-8111-111111111111").ToString();
    private static AccountInventoryItem Account(string user = "hermano@example.invalid",
        string kind = "member", bool enabled = true, bool isProtected = false, bool managed = true,
        string origin = "managed")
        => new(Subject, user, "Hermano sintético", enabled, kind, managed, isProtected, origin);

    private static readonly AccountLifecycleEvidence Verified = new(
        RolesReadFromKeycloak:true, IdentityStateVerified:true,
        CurrentEntitlementVerified:true, RevocationGuaranteed:true,
        HistoricalDataRetentionVerified:true, EffectiveRoleEvidenceComplete:true);

    [Fact]
    public void Unique_master_admin_is_always_protected()
    {
        var master = Account(user:"admin");
        foreach (var action in Enum.GetValues<AccountLifecycleAction>())
            Assert.Equal("protected",AccountLifecyclePolicy.Evaluate(
                action,master,null,Verified,"admin").DenialCode);
    }

    [Fact]
    public void Managed_account_named_qa_admin_cannot_impersonate_legacy_exception()
    {
        var account = Account(user:"qa.admin");
        foreach (var action in Enum.GetValues<AccountLifecycleAction>())
            Assert.Equal("protected",AccountLifecyclePolicy.Evaluate(
                action,account,null,Verified,"qa.admin").DenialCode);
    }

    [Fact]
    public void Verified_legacy_qa_admin_can_be_deleted_but_not_activated_or_deactivated()
    {
        var legacy = Account(user:"qa.admin",kind:"legacy_protected",
            managed:false,isProtected:true,origin:"provisioned");
        var verified = Verified with {
            RolesReadFromKeycloak=true,
            HasPrivilegedEffectiveRole=true,
            LegacyQaAdminSubjectVerified=true,
            UniqueMasterSubjectVerifiedDistinct=true,
            EffectiveRoleEvidenceComplete=true
        };
        Assert.True(AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,legacy,
            "22222222-2222-4222-8222-222222222222",verified,"qa.admin").Allowed);
        Assert.Equal("protected",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Activate,legacy,null,verified).DenialCode);
        Assert.Equal("protected",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Deactivate,legacy,null,verified).DenialCode);
    }

    [Theory]
    [InlineData(false,true,true)]
    [InlineData(true,false,true)]
    [InlineData(true,true,false)]
    public void Legacy_qa_admin_fails_closed_when_evidence_is_missing(
        bool targetVerified,bool masterVerified,bool rolesComplete)
    {
        var legacy=Account(user:"qa.admin",kind:"legacy_protected",
            managed:false,isProtected:true,origin:"provisioned");
        var evidence=Verified with {
            LegacyQaAdminSubjectVerified=targetVerified,
            UniqueMasterSubjectVerifiedDistinct=masterVerified,
            EffectiveRoleEvidenceComplete=rolesComplete
        };
        Assert.Equal("legacy_exception_unverified",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,legacy,null,evidence,"qa.admin").DenialCode);
    }

    [Fact]
    public void Qa_admin_exception_rejects_wrong_confirmation_self_and_residual_token()
    {
        var legacy=Account(user:"qa.admin",kind:"legacy_protected",
            managed:false,isProtected:true,origin:"provisioned");
        var evidence=Verified with {
            LegacyQaAdminSubjectVerified=true,
            UniqueMasterSubjectVerifiedDistinct=true,
            EffectiveRoleEvidenceComplete=true
        };
        Assert.Equal("confirmation_mismatch",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,legacy,null,evidence,"admin").DenialCode);
        Assert.Equal("self",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,legacy,Subject,evidence,"qa.admin").DenialCode);
        Assert.Equal("revocation_unverified",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,legacy,null,
            evidence with { RevocationGuaranteed=false },"qa.admin").DenialCode);
    }

    [Fact]
    public void Legacy_and_unmanaged_are_never_mutable()
    {
        foreach (var target in new[]
        {
            Account(isProtected:true),
            Account(managed:false),
            Account(origin:"provisioned"),
            Account(kind:"platform"),
            Account(kind:"legacy_protected")
        })
        {
            foreach (var action in Enum.GetValues<AccountLifecycleAction>())
                Assert.Equal("protected", AccountLifecyclePolicy.Evaluate(action,target,null,Verified,target.Username).DenialCode);
        }
    }

    [Fact]
    public void Privileged_or_unverified_keycloak_roles_deny_all_operations()
    {
        foreach (var evidence in new[]
        {
            Verified with { RolesReadFromKeycloak = false },
            Verified with { HasPrivilegedEffectiveRole = true },
            Verified with { IdentityStateVerified = false },
            Verified with { EffectiveRoleEvidenceComplete = false }
        })
        {
            foreach (var action in Enum.GetValues<AccountLifecycleAction>())
                Assert.False(AccountLifecyclePolicy.Evaluate(action,Account(enabled:action != AccountLifecycleAction.Activate),
                    null,evidence,"hermano@example.invalid").Allowed);
        }
    }

    [Fact]
    public void Revocation_must_be_proven_before_deactivation_and_deletion()
    {
        var notRevoked = Verified with { RevocationGuaranteed = false };
        Assert.Equal("revocation_unverified",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Deactivate,Account(),null,notRevoked).DenialCode);
        Assert.Equal("revocation_unverified",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,Account(),null,notRevoked,"hermano@example.invalid").DenialCode);
    }

    [Fact]
    public void Activation_requires_current_entitlement_and_inactive_state()
    {
        Assert.Equal("already_active",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Activate,Account(),null,Verified).DenialCode);
        Assert.Equal("entitlement_unverified",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Activate,Account(enabled:false),null,
            Verified with { CurrentEntitlementVerified = false }).DenialCode);
        Assert.True(AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Activate,Account(enabled:false),null,Verified).Allowed);
    }

    [Fact]
    public void Permanent_deletion_requires_exact_confirmation_and_retention()
    {
        Assert.Equal("confirmation_mismatch",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,Account(),null,Verified,"otro").DenialCode);
        Assert.Equal("retention_unverified",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,Account(),null,
            Verified with { HistoricalDataRetentionVerified = false },"hermano@example.invalid").DenialCode);
        Assert.True(AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.DeletePermanently,Account(),null,Verified,"hermano@example.invalid").Allowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" corto ")]
    [InlineData("motivo\ncon\nsaltos")]
    public void Missing_short_or_control_character_reason_is_rejected(string? reason)
        => Assert.Equal("invalid_reason", AccountLifecyclePolicy.ValidateReason(reason).DenialCode);

    [Fact]
    public void Ordinary_reason_is_accepted_but_oversize_is_denied()
    {
        Assert.True(AccountLifecyclePolicy.ValidateReason("Solicito gestión de acceso de prueba.").Allowed);
        Assert.Equal("invalid_reason",
            AccountLifecyclePolicy.ValidateReason(new string('x',201)).DenialCode);
    }

    [Fact]
    public void Self_mutation_and_unknown_subject_fail_closed()
    {
        Assert.Equal("self",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Deactivate,Account(),Subject,Verified).DenialCode);
        Assert.Equal("unknown",AccountLifecyclePolicy.Evaluate(
            AccountLifecycleAction.Deactivate,null,null,Verified).DenialCode);
    }
}
