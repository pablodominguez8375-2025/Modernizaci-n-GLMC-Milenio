using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyTreasurySignedReviewPostgreSqlTests
{
    private static PostgresException DatabaseError(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is PostgresException pg) return pg;
        throw new Xunit.Sdk.XunitException("Se esperaba rechazo desde PostgreSQL");
    }

    [Fact]
    public async Task Signed_review_history_is_immutable_and_cannot_cross_workshops_or_actors()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(cs).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);
        Assert.Contains("20261019210000_AddAssemblyTreasurySignedReviews",
            await db.Database.GetAppliedMigrationsAsync(ct));

        var workshop = Guid.NewGuid();
        var signedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var assembly = new AssemblyRecord
        {
            Name = "Asamblea C10 exclusivamente sintética",
            MeetingDate = new DateOnly(2026, 12, 30),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "test-c10"
        };
        var proposal = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assembly.Id, OrganizationId = workshop,
            Action = "proposed", ActorSubject = "gt-uno",
            OccurredAtUtc = signedAt.AddMinutes(-10),
            CoverageFrom = new DateOnly(2026, 1, 1),
            CutoffDate = assembly.CutoffDate,
            Currency = "CLP", OpeningPendingAmount = 0m,
            SourceEvidenceReference = "GT-REF-FICTICIA",
            EvidenceDigestSha256 = new string('a', 64)
        };
        db.AddRange(assembly, proposal);
        await db.SaveChangesAsync(ct);
        var review = new AssemblyTreasuryCertificationEventRecord
        {
            AssemblyId = assembly.Id, OrganizationId = workshop,
            Action = "reviewed", ActorSubject = "gt-dos",
            OccurredAtUtc = signedAt.AddMinutes(-5),
            ProposalEventId = proposal.Id,
            ReviewEvidenceReference = "GT-REV-FICTICIA",
            ReviewNote = "Solo revisión independiente"
        };
        db.Add(review);
        await db.SaveChangesAsync(ct);
        // Inserción ORM comprueba estructura SQL, NO autentica la firma.
        var attestation = new AssemblyTreasurySignedReviewRecord
        {
            AssemblyId = assembly.Id, OrganizationId = workshop,
            ProposalEventId = proposal.Id, ReviewEventId = review.Id,
            ReviewerSubject = "gt-dos",
            TrustedKeyId = "gt-key-sintetica",
            DocumentSha256Hex = new string('a', 64),
            SignatureBase64 = new string('A', 256),
            SignedAtUtc = signedAt, RecordedAtUtc = signedAt.AddSeconds(2)
        };
        db.AssemblyTreasurySignedReviews.Add(attestation);
        await db.SaveChangesAsync(ct);

        Assert.Single(await db.AssemblyTreasurySignedReviews.AsNoTracking()
            .Where(x => x.AssemblyId == assembly.Id).ToArrayAsync(ct));

        var updated = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE core.assembly_treasury_signed_reviews SET \"ReviewerSubject\" = {"intruso"} WHERE \"Id\" = {attestation.Id}", ct));
        Assert.NotNull(updated);
        Assert.Equal("55000", DatabaseError(updated).SqlState);

        var deleted = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.assembly_treasury_signed_reviews WHERE \"Id\" = {attestation.Id}", ct));
        Assert.NotNull(deleted);
        Assert.Equal("55000", DatabaseError(deleted).SqlState);

        async Task<Exception?> Insert(Guid org, Guid proposed, Guid reviewed, string subject, string hash) =>
            await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO core.assembly_treasury_signed_reviews (\"Id\",\"AssemblyId\",\"OrganizationId\",\"ProposalEventId\",\"ReviewEventId\",\"ReviewerSubject\",\"TrustedKeyId\",\"DocumentSha256Hex\",\"SignatureBase64\",\"SignedAtUtc\",\"RecordedAtUtc\") VALUES ({Guid.NewGuid()},{assembly.Id},{org},{proposed},{reviewed},{subject},{"gt-key-sintetica"},{hash},{new string('A',256)},{signedAt},{signedAt.AddSeconds(2)})", ct));

        var duplicate = await Insert(workshop, proposal.Id, review.Id, "gt-dos", new string('a', 64));
        Assert.NotNull(duplicate);
        Assert.Equal("23505", DatabaseError(duplicate).SqlState);

        var wrongWorkshop = await Insert(Guid.NewGuid(), proposal.Id, review.Id, "gt-dos", new string('a', 64));
        Assert.NotNull(wrongWorkshop);
        Assert.Equal("23514", DatabaseError(wrongWorkshop).SqlState);

        var wrongReviewer = await Insert(workshop, proposal.Id, review.Id, "gt-uno", new string('a', 64));
        Assert.NotNull(wrongReviewer);
        Assert.Equal("23514", DatabaseError(wrongReviewer).SqlState);

        var badHash = await Insert(workshop, proposal.Id, review.Id, "gt-dos", new string('b', 64));
        Assert.NotNull(badHash);
        Assert.Equal("23514", DatabaseError(badHash).SqlState);
    }

    [Fact]
    public async Task Cannot_store_signature_as_financial_certification_or_without_valid_shape()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(cs).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);
        // SQL deliberately has no "certified" status, balance or voting field.
        var tableColumns = await db.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema='core' AND table_name='assembly_treasury_signed_reviews'")
            .ToArrayAsync(ct);
        Assert.DoesNotContain("MayAuthorizeSuffrage", tableColumns);
        Assert.DoesNotContain("AmountPending", tableColumns);
        Assert.DoesNotContain("Certified", tableColumns);
    }
}
