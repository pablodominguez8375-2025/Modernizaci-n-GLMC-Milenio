using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyGrandTreasuryReviewEventsPostgreSqlTests
{
    [Fact]
    public async Task Proposals_and_independent_review_are_append_only_and_tenant_scoped()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);
        Assert.Contains("20261019190000_AddAssemblyGrandTreasuryReviewEvents",
            await db.Database.GetAppliedMigrationsAsync(ct));

        var assembly = new AssemblyRecord
        {
            Name = "Gran Asamblea sintética GT eventos",
            MeetingDate = new DateOnly(2026, 12, 30),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "test-gt"
        };
        var workshop = Guid.NewGuid();
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assembly.Id,
            OrganizationId = workshop,
            Action = "proposed",
            ActorSubject = "gt-proponente",
            CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = assembly.CutoffDate,
            Currency = "CLP",
            OpeningPendingAmount = 100m,
            SourceEvidenceReference = "GT-PL-FICTICIA",
            EvidenceDigestSha256 = new string('c', 64)
        };
        db.AddRange(assembly, proposal);
        await db.SaveChangesAsync(ct);
        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assembly.Id,
            OrganizationId = workshop,
            Action = "reviewed",
            ActorSubject = "gt-revisor-distinto",
            ProposalEventId = proposal.Id,
            ReviewEvidenceReference = "GT-REVISION-FICTICIA",
            ReviewNote = "Revisado, sin valor de certificación final"
        };
        db.Add(review);
        await db.SaveChangesAsync(ct);
        Assert.Equal(2, await db.AssemblyTreasuryCertificationEvents.AsNoTracking()
            .CountAsync(x => x.AssemblyId == assembly.Id, ct));

        static PostgresException Extract(Exception e)
        {
            for (Exception? inner = e; inner is not null; inner = inner.InnerException)
                if (inner is PostgresException pg) return pg;
            throw new Xunit.Sdk.XunitException("Expected Npgsql PostgreSQL error");
        }

        var edited = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE core.assembly_treasury_certification_events SET \"ActorSubject\" = {"cambiado"} WHERE \"Id\" = {proposal.Id}", ct));
        Assert.NotNull(edited);
        Assert.Equal("55000", Extract(edited).SqlState);
        var deleted = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.assembly_treasury_certification_events WHERE \"Id\" = {review.Id}", ct));
        Assert.NotNull(deleted);
        Assert.Equal("55000", Extract(deleted).SqlState);

        var duplicateReview = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO core.assembly_treasury_certification_events (\"Id\",\"AssemblyId\",\"OrganizationId\",\"Action\",\"ActorSubject\",\"OccurredAtUtc\",\"ProposalEventId\",\"ReviewEvidenceReference\",\"ReviewNote\") VALUES ({Guid.NewGuid()},{assembly.Id},{workshop},{"rejected"},{"gt-tercero"},{DateTimeOffset.UtcNow},{proposal.Id},{"GT-OTRA-REVISION"},{"Segundo resultado inválido"})", ct));
        Assert.NotNull(duplicateReview);
        Assert.Equal("23505", Extract(duplicateReview).SqlState);

        var selfReview = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO core.assembly_treasury_certification_events (\"Id\",\"AssemblyId\",\"OrganizationId\",\"Action\",\"ActorSubject\",\"OccurredAtUtc\",\"ProposalEventId\",\"ReviewEvidenceReference\",\"ReviewNote\") VALUES ({Guid.NewGuid()},{assembly.Id},{workshop},{"reviewed"},{"gt-proponente"},{DateTimeOffset.UtcNow},{proposal.Id},{"GT-SELF"},{"Mismo funcionario"})", ct));
        Assert.NotNull(selfReview);
        Assert.Equal("23514", Extract(selfReview).SqlState);

        var foreignWorkshop = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO core.assembly_treasury_certification_events (\"Id\",\"AssemblyId\",\"OrganizationId\",\"Action\",\"ActorSubject\",\"OccurredAtUtc\",\"ProposalEventId\",\"ReviewEvidenceReference\",\"ReviewNote\") VALUES ({Guid.NewGuid()},{assembly.Id},{Guid.NewGuid()},{"reviewed"},{"gt-ajeno"},{DateTimeOffset.UtcNow},{proposal.Id},{"GT-TALLER-AJENO"},{"Cruce de taller"})", ct));
        Assert.NotNull(foreignWorkshop);
        Assert.Equal("23514", Extract(foreignWorkshop).SqlState);
    }

    [Fact]
    public async Task Direct_insert_of_fake_final_certificate_or_null_evidence_is_blocked()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);

        var assembly = new AssemblyRecord
        {
            Name = "Gran Asamblea negativa",
            MeetingDate = new DateOnly(2026, 12, 30),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "test-gt"
        };
        db.Assemblies.Add(assembly);
        await db.SaveChangesAsync(ct);

        async Task CheckRejected(string action)
        {
            var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO core.assembly_treasury_certification_events (\"Id\",\"AssemblyId\",\"OrganizationId\",\"Action\",\"ActorSubject\",\"OccurredAtUtc\",\"CoverageFrom\",\"CutoffDate\",\"Currency\",\"OpeningPendingAmount\") VALUES ({Guid.NewGuid()},{assembly.Id},{Guid.NewGuid()},{action},{"gt-emisor"},{DateTimeOffset.UtcNow},{new DateOnly(2026,1,1)},{new DateOnly(2026,10,19)},{"CLP"},{0m})", ct));
            Assert.NotNull(error);
            var pg = error;
            while (pg is not null && pg is not PostgresException) pg = pg.InnerException;
            Assert.IsType<PostgresException>(pg);
            Assert.Equal("23514", ((PostgresException)pg).SqlState);
        }
        await CheckRejected("certified");
        await CheckRejected("proposed"); // propuesta sin documento ni hash
    }
}
