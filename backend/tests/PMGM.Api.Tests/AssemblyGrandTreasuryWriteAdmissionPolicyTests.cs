using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyGrandTreasuryWriteAdmissionPolicyTests
{
    private static readonly Guid Lodge = Guid.Parse("3610353e-17e8-4a85-a0b8-27c806e7b987");
    private static readonly DateOnly From = new(2026, 8, 1);
    private static readonly DateOnly Cutoff = new(2026, 10, 19);
    private static readonly DateTimeOffset Now =
        new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Doc =
        Encoding.UTF8.GetBytes("Documento sintético sin datos sensibles");

    private static ClaimsPrincipal Actor(string subject = "gt-proponente",
        string role = InstitutionalRoles.GranTesoreria,
        bool scope = true, bool authenticated = true)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new(InstitutionalClaims.Role, role)
        };
        if (scope) claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticated ? "synthetic-test" : null));
    }

    private static DynamicAccessCatalog Catalog(
        string subject = "gt-proponente",
        string[]? actions = null,
        bool activeAssignment = true,
        bool activeProfile = true,
        DateOnly? expires = null,
        bool lodgeScope = false)
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        catalog.Profiles.Add(new DynamicProfile(Guid.NewGuid(),
            "gt-padrón", "Gran Tesorería padrón",
            lodgeScope ? "lodge" : "order", false, activeProfile, ["treasury"],
            [new DynamicGrant("treasury", actions ?? ["view", "write"])]));
        catalog.Assignments.Add(new DynamicAssignment(Guid.NewGuid(),
            subject, "gt-padrón", lodgeScope ? Lodge : null,
            From, expires, activeAssignment));
        return catalog;
    }

    private static AssemblyTreasuryInspectionResult Inspection() =>
        new(Lodge, From, Cutoff, 3, 3, true, 0m,
            ["HISTORICAL_CERTIFICATION_REQUIRED"]);

    private static AssemblyGrandTreasuryEvidenceProposal Evidence() =>
        new(Lodge, From, Cutoff, "CLP", From.AddDays(-1), 0m,
            "GT-APERTURA-SINTETICA", "GT-RESOLUCION-SINTETICA",
            "GT-RECTIFICACIONES-SINTETICAS", new string('b', 64),
            true, true, true);

    private static AssemblyGrandTreasuryWriteAdmissionResult Proposal(
        ClaimsPrincipal? actor = null, DynamicAccessCatalog? catalog = null,
        AssemblyGrandTreasuryEvidenceProposal? evidence = null) =>
        AssemblyGrandTreasuryWriteAdmissionPolicy.AssessProposal(
            actor ?? Actor(), catalog ?? Catalog(), Cutoff,
            Inspection(), evidence ?? Evidence());

    [Fact]
    public void Explicit_grant_and_complete_preflight_are_ready_only_for_audited_processing()
    {
        var result = Proposal();
        Assert.True(result.ReadyForAuditedProcessing);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void Missing_dynamic_enrolment_never_uses_legacy_allow_fallback()
    {
        var empty = DynamicAccessEndpoints.CreateCatalog();
        Assert.True(DynamicGrandTreasuryAccess.Allows(empty, "gt-proponente", "write", Cutoff));
        var denied = Proposal(catalog: empty);
        Assert.False(denied.ReadyForAuditedProcessing);
        Assert.False(denied.MayAuthorizeSuffrage);
        Assert.Contains("EXPLICIT_DYNAMIC_WRITE_GRANT_REQUIRED", denied.BlockingReasons);
    }

    [Theory]
    [InlineData(InstitutionalRoles.PlatformSuperAdmin)]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    [InlineData(InstitutionalRoles.RegimenInterior)]
    public void Administrators_and_other_roles_are_not_grand_treasury(string role)
    {
        var r = Proposal(actor: Actor(role: role));
        Assert.False(r.ReadyForAuditedProcessing);
        Assert.Contains("EXPLICIT_GRAND_TREASURY_ROLE_REQUIRED", r.BlockingReasons);
    }

    [Fact]
    public void Institutional_scope_and_authenticated_identity_are_mandatory()
    {
        Assert.False(Proposal(actor: Actor(scope: false)).ReadyForAuditedProcessing);
        Assert.False(Proposal(actor: Actor(authenticated: false)).ReadyForAuditedProcessing);
    }

    [Fact]
    public void View_only_expired_inactive_and_lodge_grants_do_not_authorize_writes()
    {
        var alternatives = new[]
        {
            Catalog(actions: ["view"]),
            Catalog(expires: Cutoff.AddDays(-1)),
            Catalog(activeAssignment: false),
            Catalog(activeProfile: false),
            Catalog(lodgeScope: true),
            Catalog(subject: "otro-usuario")
        };
        foreach (var grant in alternatives)
        {
            var result = Proposal(catalog: grant);
            Assert.False(result.ReadyForAuditedProcessing);
            Assert.Contains("EXPLICIT_DYNAMIC_WRITE_GRANT_REQUIRED", result.BlockingReasons);
        }
    }

    [Fact]
    public void Incomplete_documentation_blocks_even_when_dynamic_permissions_pass()
    {
        var result = Proposal(evidence: Evidence() with { CompleteDebtHistoryReviewed = false });
        Assert.Contains("PROPOSAL_DOCUMENTS_NOT_READY", result.BlockingReasons);
        Assert.False(result.MayAuthorizeSuffrage);
    }

    private static (AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review) Events()
    {
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = Guid.NewGuid(), OrganizationId = Lodge,
            Action = "proposed", ActorSubject = "gt-proponente",
            OccurredAtUtc = Now.AddMinutes(-10),
            CoverageFrom = From, CutoffDate = Cutoff,
            Currency = "CLP", OpeningPendingAmount = 0m,
            SourceEvidenceReference = "GT-RES-SINTETICA",
            EvidenceDigestSha256 = Convert.ToHexString(SHA256.HashData(Doc)).ToLowerInvariant()
        };
        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = proposal.AssemblyId, OrganizationId = Lodge,
            Action = "reviewed", ActorSubject = "gt-revisor",
            ProposalEventId = proposal.Id,
            OccurredAtUtc = Now.AddMinutes(-5),
            ReviewEvidenceReference = "REVISION-GT-SINTETICA",
            ReviewNote = "Solo comprobación del respaldo"
        };
        return (proposal, review);
    }

    private static (AssemblyGrandTreasuryReviewSignature signature,
        AssemblyGrandTreasuryServerTrustRegistry registry) SignedEvidence(
            AssemblyTreasuryCertificationEventRecord proposal,
            AssemblyTreasuryCertificationEventRecord review, RSA rsa)
    {
        var unsigned = new AssemblyGrandTreasuryReviewSignature(
            proposal.AssemblyId, proposal.OrganizationId, proposal.Id, review.Id,
            "gt-revisor", "gt-clave-c8", Now.AddMinutes(-2), "");
        var signed = unsigned with
        {
            SignatureBase64 = Convert.ToBase64String(rsa.SignData(
                AssemblyGrandTreasuryReviewSignaturePolicy
                    .BuildCanonicalPayload(proposal, review, unsigned),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
        };
        const string prefix =
            AssemblyGrandTreasuryServerTrustRegistry.ConfigurationSection + ":0:";
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [prefix + "KeyId"] = "gt-clave-c8",
                [prefix + "ReviewerSubject"] = "gt-revisor",
                [prefix + "PublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
                [prefix + "ValidFromUtc"] = Now.AddDays(-1).ToString("O"),
                [prefix + "ValidUntilUtc"] = Now.AddDays(1).ToString("O"),
                [prefix + "Approved"] = "true"
            }).Build();
        return (signed, new AssemblyGrandTreasuryServerTrustRegistry(config));
    }

    [Fact]
    public void Independent_reviewer_with_exact_grant_and_valid_signature_can_only_stage_review()
    {
        using var rsa = RSA.Create(2048);
        var (proposal, review) = Events();
        var (sig, registry) = SignedEvidence(proposal, review, rsa);
        var result = AssemblyGrandTreasuryWriteAdmissionPolicy.AssessSignedReview(
            Actor("gt-revisor"), Catalog("gt-revisor"), Cutoff,
            proposal, review, sig, Doc, registry, Now);
        Assert.True(result.ReadyForAuditedProcessing);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void Reviewer_must_match_both_signature_and_authenticated_claims()
    {
        using var rsa = RSA.Create(2048);
        var (proposal, review) = Events();
        var (sig, registry) = SignedEvidence(proposal, review, rsa);
        var wrongActor = AssemblyGrandTreasuryWriteAdmissionPolicy.AssessSignedReview(
            Actor("gt-proponente"), Catalog("gt-proponente"), Cutoff,
            proposal, review, sig, Doc, registry, Now);
        Assert.Contains("REVIEWER_ACTOR_MISMATCH", wrongActor.BlockingReasons);

        var noEnrollment = AssemblyGrandTreasuryWriteAdmissionPolicy.AssessSignedReview(
            Actor("gt-revisor"), DynamicAccessEndpoints.CreateCatalog(), Cutoff,
            proposal, review, sig, Doc, registry, Now);
        Assert.Contains("EXPLICIT_DYNAMIC_WRITE_GRANT_REQUIRED", noEnrollment.BlockingReasons);

        var invalidSignature = AssemblyGrandTreasuryWriteAdmissionPolicy.AssessSignedReview(
            Actor("gt-revisor"), Catalog("gt-revisor"), Cutoff,
            proposal, review, sig with { SignatureBase64 = "alterada" },
            Doc, registry, Now);
        Assert.Contains("SIGNED_REVIEW_NOT_VERIFIED", invalidSignature.BlockingReasons);
        Assert.False(invalidSignature.MayAuthorizeSuffrage);
    }
}
