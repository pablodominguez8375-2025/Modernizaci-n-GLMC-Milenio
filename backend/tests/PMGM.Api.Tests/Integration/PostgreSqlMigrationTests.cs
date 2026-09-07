using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.Privacy.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

public sealed class PostgreSqlMigrationTests
{
    [Fact]
    public async Task Migrations_apply_and_basic_roundtrip_works()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(options);

        await db.Database.MigrateAsync(cancellationToken);

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        Assert.Empty(pendingMigrations);

        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260907122500_InitialCore", appliedMigrations);
        Assert.Contains("20260907132500_AddCeremonyEligibility", appliedMigrations);
        Assert.Contains("20260907141000_AddAuditAndPrivacyCompliance", appliedMigrations);
        Assert.Contains("20260907154500_AddRetentionHoldsAndEvaluations", appliedMigrations);
        Assert.Contains("20260907155000_ExpandPrivacyIncidentWorkflow", appliedMigrations);
        Assert.Contains("20260907160000_AddRetentionExecutionState", appliedMigrations);

        var organization = new Organization
        {
            Name = $"CI PostgreSQL {Guid.NewGuid():N}",
            Number = "CI",
            Type = "workshop"
        };

        db.Organizations.Add(organization);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var persisted = await db.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Id == organization.Id, cancellationToken);

        Assert.Equal(organization.Name, persisted.Name);
        Assert.Equal(organization.Number, persisted.Number);
        Assert.Equal(organization.Type, persisted.Type);

        _ = await db.CandidatePublications.AsNoTracking().CountAsync(cancellationToken);
        _ = await db.AuditEvents.AsNoTracking().CountAsync(cancellationToken);
        _ = await db.PrivacySecurityIncidents.AsNoTracking().CountAsync(cancellationToken);
        _ = await db.Set<DataRetentionEvaluation>().AsNoTracking().CountAsync(cancellationToken);
    }

    [Fact]
    public async Task Ceremony_evidence_and_audit_persist_on_real_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var organization = new Organization
        {
            Name = $"Taller CI {Guid.NewGuid():N}",
            Number = "23-CI",
            Type = "workshop"
        };
        var candidate = new Person
        {
            FirstNames = "Insinuado",
            LastNames = "Prueba"
        };

        db.AddRange(organization, candidate);
        await db.SaveChangesAsync(cancellationToken);

        var ceremony = new CeremonyRequest
        {
            OrganizationId = organization.Id,
            CeremonyType = CeremonyCodes.Type.Initiation,
            CandidatePersonId = candidate.Id,
            ProposedDate = today.AddDays(5),
            Status = CeremonyCodes.RequestStatus.UnderReview
        };
        var treasury = new FinancialRegularitySnapshot
        {
            OrganizationId = organization.Id,
            Scope = TreasuryCodes.RegularityScope.Organization,
            Status = TreasuryCodes.RegularityStatus.UpToDate,
            AsOfDate = today
        };
        var hospitalaria = new HospitalariaRegularitySnapshot
        {
            OrganizationId = organization.Id,
            Status = HospitalariaCodes.RegularityStatus.UpToDate,
            AsOfDate = today
        };

        db.AddRange(ceremony, treasury, hospitalaria);
        await db.SaveChangesAsync(cancellationToken);

        var internalAffairs = new CeremonyValidation
        {
            CeremonyRequestId = ceremony.Id,
            ValidationType = CeremonyCodes.ValidationType.InternalAffairs,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = today
        };
        var publication = new CandidatePublication
        {
            CeremonyRequestId = ceremony.Id,
            PersonId = candidate.Id,
            OrganizationId = organization.Id,
            PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(-21),
            RequiredDays = 20,
            RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            Status = CeremonyCodes.PublicationStatus.Published
        };

        db.AddRange(internalAffairs, publication);

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "ci-default-correlation",
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim("sub", "ci-actor"),
                    new Claim(ClaimTypes.Name, "CI Integration")
                },
                "test"))
        };
        httpContext.Request.Headers["X-Correlation-ID"] = "ci-postgres-ceremony";

        var audit = new AuditService(db);
        audit.Add(
            httpContext,
            "ceremony.integration.persisted",
            nameof(CeremonyRequest),
            ceremony.Id.ToString(),
            organization.Id,
            AuditResults.Success,
            new { ceremony.CeremonyType, publication.RequiredDays });

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var persistedTreasury = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .SingleAsync(x => x.Id == treasury.Id, cancellationToken);
        var persistedHospitalaria = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .SingleAsync(x => x.Id == hospitalaria.Id, cancellationToken);
        var persistedValidation = await db.CeremonyValidations
            .AsNoTracking()
            .SingleAsync(x => x.Id == internalAffairs.Id, cancellationToken);
        var persistedPublication = await db.CandidatePublications
            .AsNoTracking()
            .SingleAsync(x => x.Id == publication.Id, cancellationToken);

        var decision = CeremonyEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Initiation,
            persistedValidation.Status,
            persistedTreasury.Status,
            persistedHospitalaria.Status,
            new CandidatePublicationEvidence(
                persistedPublication.Id,
                persistedPublication.Status,
                persistedPublication.RequiredDays,
                21,
                persistedPublication.RuleCode));

        Assert.True(decision.CanAuthorize);

        var auditEvent = await db.AuditEvents
            .AsNoTracking()
            .SingleAsync(x => x.EntityId == ceremony.Id.ToString() && x.Action == "ceremony.integration.persisted", cancellationToken);

        Assert.Equal("ci-actor", auditEvent.ActorSubject);
        Assert.Equal("CI Integration", auditEvent.ActorDisplayName);
        Assert.Equal("ci-postgres-ceremony", auditEvent.CorrelationId);
        Assert.Equal(AuditResults.Success, auditEvent.Result);
        Assert.Equal(organization.Id, auditEvent.OrganizationId);
    }
}
