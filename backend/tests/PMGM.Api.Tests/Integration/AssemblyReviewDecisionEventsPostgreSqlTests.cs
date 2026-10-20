using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyReviewDecisionEventsPostgreSqlTests
{
    [Fact]
    public async Task Ri_decisions_are_append_only_tenant_safe_and_never_approve_voting()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(cs).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);
        Assert.Contains("20261020090000_AddAssemblyReviewDecisionEvents",
            await db.Database.GetAppliedMigrationsAsync(ct));

        var assembly = new AssemblyRecord
        {
            Name = "Gran Asamblea RI C15 sintética",
            MeetingDate = new DateOnly(2026, 12, 30),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "ri-prueba"
        };
        var member = new AssemblyMemberStatusRecord
        {
            AssemblyId = assembly.Id,
            PersonId = Guid.NewGuid(), MemberId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(), Category = "representative",
            SourceOfficeAssignmentId = Guid.NewGuid(),
            SourceEvidenceReference = "Cargo sintético C15",
            LastNamesAtCutoff = "Apellido ficticio",
            FirstNamesAtCutoff = "Nombre ficticio",
            RutAtCutoff = "FICTICIO",
            WorkshopAtCutoff = "Taller sintético",
            VerifiedAtUtc = DateTimeOffset.UtcNow
        };
        db.AddRange(assembly, member);
        await db.SaveChangesAsync(ct);

        var evt = new AssemblyReviewDecisionEventRecord
        {
            AssemblyId = assembly.Id, MemberStatusId = member.Id,
            Action = "request_correction",
            Reason = "Verificar antecedentes pendientes",
            EvidenceReference = "RI-PL-TEST",
            DecidedBySubject = "ri-prueba"
        };
        db.AssemblyReviewDecisionEvents.Add(evt);
        await db.SaveChangesAsync(ct);
        Assert.Equal(1, await db.AssemblyReviewDecisionEvents.AsNoTracking()
            .CountAsync(x => x.Id == evt.Id, ct));

        static string PostgresState(Exception? ex)
        {
            for (var e = ex; e is not null; e = e.InnerException)
                if (e is PostgresException pg) return pg.SqlState;
            throw new Xunit.Sdk.XunitException("Expected a PostgreSQL constraint error");
        }

        var updated = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE core.assembly_review_decision_events SET \"Action\" = {"attendance_only"} WHERE \"Id\" = {evt.Id}", ct));
        Assert.Equal("55000", PostgresState(updated));
        var deleted = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.assembly_review_decision_events WHERE \"Id\" = {evt.Id}", ct));
        Assert.Equal("55000", PostgresState(deleted));

        var invalidAction = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO core.assembly_review_decision_events (\"Id\",\"AssemblyId\",\"MemberStatusId\",\"Action\",\"Reason\",\"EvidenceReference\",\"DecidedBySubject\",\"OccurredAtUtc\") VALUES ({Guid.NewGuid()},{assembly.Id},{member.Id},{"confirm_eligible"},{"Motivo ficticio"},{"RI-TEST"},{"ri-prueba"},{DateTimeOffset.UtcNow})", ct));
        Assert.Equal("23514", PostgresState(invalidAction));

        var foreignMember = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO core.assembly_review_decision_events (\"Id\",\"AssemblyId\",\"MemberStatusId\",\"Action\",\"Reason\",\"EvidenceReference\",\"DecidedBySubject\",\"OccurredAtUtc\") VALUES ({Guid.NewGuid()},{assembly.Id},{Guid.NewGuid()},{"refer_validation"},{"Motivo ficticio"},{"RI-TEST"},{"ri-prueba"},{DateTimeOffset.UtcNow})", ct));
        Assert.Equal("23503", PostgresState(foreignMember));
    }
}
