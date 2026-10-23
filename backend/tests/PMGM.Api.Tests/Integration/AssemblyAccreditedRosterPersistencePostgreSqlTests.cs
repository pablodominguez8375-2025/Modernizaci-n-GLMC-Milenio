using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyAccreditedRosterPersistencePostgreSqlTests
{
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static DefaultHttpContext Context(string role = InstitutionalRoles.RegimenInterior)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "synthetic-ri-c23"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, role)
        ], "synthetic-qa"));
        return context;
    }

    private static AssemblyRecord Draft() => new()
    {
        Name = "Gran Asamblea C23 ficticia",
        CutoffDate = Cutoff,
        MeetingDate = Cutoff.AddMonths(2),
        CreatedBySubject = "synthetic-ri-c23"
    };

    private static AssemblyMemberStatusRecord Candidate(Guid assemblyId) => new()
    {
        AssemblyId = assemblyId,
        PersonId = Guid.NewGuid(),
        MemberId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Category = AssemblyMemberCategoryPolicy.Representative,
        SourceOfficeAssignmentId = Guid.NewGuid(),
        SourceEvidenceReference = "CERTIFICACION_VM_SINTETICA_C23",
        FirstNamesAtCutoff = "Ana",
        LastNamesAtCutoff = "Pérez",
        RutAtCutoff = "12.345.678-5",
        WorkshopAtCutoff = "Taller sintético",
        VerifiedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2)
    };

    private static AssemblyAccreditedMemberMaterializationResult Prepared(
        params AssemblyMemberStatusRecord[] members) =>
        new(true, members, []);

    [Fact]
    public async Task Accredited_batch_is_atomic_pending_and_audited_then_reimport_is_denied()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;

        await using (var core = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connection).Options))
            await core.Database.MigrateAsync(ct);

        await using var db = new RegimenInteriorDbContext(
            new DbContextOptionsBuilder<RegimenInteriorDbContext>()
                .UseNpgsql(connection).Options);
        await db.Database.MigrateAsync(ct);

        var assembly = Draft();
        db.Assemblies.Add(assembly);
        await db.SaveChangesAsync(ct);
        var first = Candidate(assembly.Id);
        var second = Candidate(assembly.Id);
        var batch = Prepared(first, second);

        var imported = await AssemblyAccreditedRosterPersistenceService.PersistAsync(
            assembly.Id, Cutoff, batch, Context(), db, ct);
        Assert.True(imported.Persisted);
        Assert.Equal(2, imported.MemberCount);

        var members = await db.AssemblyMemberStatuses.AsNoTracking()
            .Where(m => m.AssemblyId == assembly.Id).ToArrayAsync(ct);
        Assert.Equal(2, members.Length);
        var evaluations = await db.AssemblyEligibilities.AsNoTracking()
            .Where(e => e.AssemblyId == assembly.Id).ToArrayAsync(ct);
        Assert.Equal(2, evaluations.Length);
        Assert.All(evaluations, e =>
        {
            Assert.Equal(AssemblyVoterEligibilityPolicy.Pending, e.Status);
            Assert.Equal(AssemblyAccreditedRosterPersistenceService.PendingReason, e.ReasonCode);
            Assert.False(e.MayVote);
            Assert.False(e.MayAttend);
            Assert.Null(e.GrandTreasuryPendingAmount);
        });

        var again = await AssemblyAccreditedRosterPersistenceService.PersistAsync(
            assembly.Id, Cutoff, batch, Context(), db, ct);
        Assert.False(again.Persisted);
        Assert.Equal("ASSEMBLY_ROSTER_ALREADY_MATERIALIZED", again.BlockingReason);
        Assert.Equal(2, await db.AssemblyEligibilities.CountAsync(
            e => e.AssemblyId == assembly.Id, ct));
        await using var auditDb = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connection).Options);
        var auditable = await auditDb.AuditEvents.AsNoTracking()
            .Where(a => a.Action == "regimen.asamblea.accredited_candidates.materialized" &&
                a.EntityId == assembly.Id.ToString("D"))
            .ToArrayAsync(ct);
        Assert.Single(auditable);
        Assert.DoesNotContain("12.345.678-5", auditable[0].MetadataJson ?? "");
        Assert.DoesNotContain(first.PersonId.ToString(), auditable[0].MetadataJson ?? "");
    }

    [Fact]
    public async Task Unauthorized_invalid_or_mismatched_batch_never_writes_candidates()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = new RegimenInteriorDbContext(
            new DbContextOptionsBuilder<RegimenInteriorDbContext>()
                .UseNpgsql(connection).Options);
        await db.Database.MigrateAsync(ct);
        var assembly = Draft();
        db.Assemblies.Add(assembly);
        await db.SaveChangesAsync(ct);

        var first = Candidate(assembly.Id);
        var unauthorized = await AssemblyAccreditedRosterPersistenceService.PersistAsync(
            assembly.Id, Cutoff, Prepared(first), Context(InstitutionalRoles.GranTesoreria),
            db, ct);
        Assert.Equal("REGIMEN_INTERIOR_AUTHORIZATION_REQUIRED", unauthorized.BlockingReason);

        var foreign = Candidate(Guid.NewGuid());
        var mismatch = await AssemblyAccreditedRosterPersistenceService.PersistAsync(
            assembly.Id, Cutoff, Prepared(first, foreign), Context(), db, ct);
        Assert.Equal("INVALID_ACCREDITED_MEMBER_BATCH", mismatch.BlockingReason);

        var duplicateRow = Candidate(assembly.Id);
        duplicateRow.PersonId = first.PersonId;
        var duplicate = await AssemblyAccreditedRosterPersistenceService.PersistAsync(
            assembly.Id, Cutoff, Prepared(first, duplicateRow),
            Context(), db, ct);
        Assert.Equal("INVALID_ACCREDITED_MEMBER_BATCH", duplicate.BlockingReason);

        var cutoff = await AssemblyAccreditedRosterPersistenceService.PersistAsync(
            assembly.Id, Cutoff.AddDays(-1), Prepared(first), Context(), db, ct);
        Assert.Equal("ASSEMBLY_STATE_OR_CUTOFF_CHANGED", cutoff.BlockingReason);

        Assert.False(await db.AssemblyMemberStatuses.AnyAsync(
            m => m.AssemblyId == assembly.Id, ct));
        Assert.False(await db.AssemblyEligibilities.AnyAsync(
            e => e.AssemblyId == assembly.Id, ct));
    }

}
