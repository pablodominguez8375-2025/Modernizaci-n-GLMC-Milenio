using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyFoundationPostgreSqlTests
{
    [Fact]
    public async Task Foundation_migrates_preserves_history_and_rejects_rewriting_debt_decisions()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);
        var migrations = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        Assert.Contains("20261019180000_AddAssemblyRosterFoundations", migrations);

        var assembly = new AssemblyRecord
        {
            Name = "Gran Asamblea ficticia",
            MeetingDate = new DateOnly(2026, 12, 31),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "qa-regimen-interior"
        };
        var person = Guid.NewGuid();
        var workshop = Guid.NewGuid();
        var member = new AssemblyMemberStatusRecord
        {
            Assembly = assembly,
            PersonId = person,
            MemberId = Guid.NewGuid(),
            OrganizationId = workshop,
            Category = "permanent_assembly_member",
            SourceOfficeAssignmentId = Guid.NewGuid(),
            SourceEvidenceReference = "PL-CARGO-VM-FICTICIO",
            FullTermVerified = true,
            FirstNamesAtCutoff = "Ana",
            LastNamesAtCutoff = "Núñez",
            RutAtCutoff = "11.111.111-1",
            WorkshopAtCutoff = "Taller Ficticio",
            VerifiedAtUtc = DateTimeOffset.UtcNow
        };
        var evaluation = new AssemblyEligibilityRecord
        {
            AssemblyId = assembly.Id,
            MemberStatus = member,
            Status = "pendiente",
            MayAttend = true,
            MayVote = false,
            ReasonCode = "SALDO_GRAN_TESORERIA_NO_ACREDITADO",
            EvaluatedBySubject = "qa-regimen-interior"
        };
        var eventItem = new AssemblyDebtDecisionEventRecord
        {
            AssemblyId = assembly.Id,
            OrganizationId = workshop,
            Action = "lift",
            Authority = "regimen_interior",
            DecidedBySubject = "qa-regimen-interior",
            Reason = "Plancha ficticia para prueba de evento inmutable",
            EvidenceReference = "PL-FICTICIA-2026"
        };
        db.AddRange(assembly, member, evaluation, eventItem);
        await db.SaveChangesAsync(ct);
        Assert.Single(await db.AssemblyEligibilities.AsNoTracking()
            .Where(x => x.MemberStatusId == member.Id).ToListAsync(ct));

        // Debe fallar desde PostgreSQL, no solo al comprobar permisos del front.
        var deniedDelete = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.assembly_debt_decision_events WHERE \"Id\" = {eventItem.Id}", ct));
        Assert.Equal("55000", deniedDelete.SqlState);

        var persisted = await db.AssemblyDebtDecisionEvents.AsNoTracking()
            .SingleAsync(x => x.Id == eventItem.Id, ct);
        Assert.Equal("lift", persisted.Action);
        Assert.Equal("PL-FICTICIA-2026", persisted.EvidenceReference);

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE core.assembly_debt_decision_events SET \"Reason\" = {'X'} WHERE \"Id\" = {eventItem.Id}", ct));
    }

    [Fact]
    public async Task Eligibility_cannot_reference_a_member_status_from_another_assembly()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);

        AssemblyRecord MakeAssembly(string name) => new()
        {
            Name = name,
            MeetingDate = new DateOnly(2026, 12, 31),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "qa-regimen-interior"
        };
        var origin = MakeAssembly("Asamblea original de ensayo");
        var target = MakeAssembly("Otra Asamblea de ensayo");
        var member = new AssemblyMemberStatusRecord
        {
            Assembly = origin,
            PersonId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Category = "representative",
            SourceOfficeAssignmentId = Guid.NewGuid(),
            SourceEvidenceReference = "PL-FICTICIA",
            FirstNamesAtCutoff = "Marta",
            LastNamesAtCutoff = "Álvarez",
            RutAtCutoff = "22.222.222-2",
            WorkshopAtCutoff = "Taller sintético",
            VerifiedAtUtc = DateTimeOffset.UtcNow
        };
        db.AddRange(origin, target, member);
        await db.SaveChangesAsync(ct);

        db.AssemblyEligibilities.Add(new AssemblyEligibilityRecord
        {
            AssemblyId = target.Id,
            MemberStatusId = member.Id,
            Status = "pendiente",
            MayAttend = false,
            MayVote = false,
            ReasonCode = "EVIDENCIA_PENDIENTE",
            EvaluatedBySubject = "qa-regimen-interior"
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal("23503", postgres.SqlState); // Violación de FK compuesta.
    }

    [Fact]
    public async Task Closed_assembly_without_evidence_is_rejected_at_database_level()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);

        var invalid = new AssemblyRecord
        {
            Name = "Intento de cierre sin evidencia",
            MeetingDate = new DateOnly(2026, 12, 31),
            CutoffDate = new DateOnly(2026, 10, 19),
            Status = "closed",
            CreatedBySubject = "qa-regimen-interior"
        };
        db.Assemblies.Add(invalid);
        var failure = await Record.ExceptionAsync(() => db.SaveChangesAsync(ct));
        Assert.NotNull(failure);
        var postgres = WalkToPostgresException(failure);
        Assert.NotNull(postgres);
        Assert.Equal("55000", postgres.SqlState); // Cierre no autorizado por el trigger.

        static PostgresException? WalkToPostgresException(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
                if (current is PostgresException postgresException)
                    return postgresException;
            return null;
        }
    }

    [Fact]
    public async Task Attestation_events_are_append_only_and_restricted_to_known_kinds()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<RegimenInteriorDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new RegimenInteriorDbContext(options);
        await db.Database.MigrateAsync(ct);
        Assert.Contains("20261026130000_AddAssemblyAttestationEvents", await db.Database.GetAppliedMigrationsAsync(ct));

        var assembly = new AssemblyRecord { Name = "GA constancias ficticia", MeetingDate = new(2026, 12, 31), CutoffDate = new(2026, 10, 19), CreatedBySubject = "qa-ri" };
        db.Assemblies.Add(assembly);
        var ev = new AssemblyAttestationEventRecord
        {
            AssemblyId = assembly.Id, Kind = "membership", Action = "attest", ActaReference = "Acta ficticia 1",
            Statement = "Declaración ficticia de prueba.", RecordedBySubject = "qa-ri", CutoffDate = assembly.CutoffDate
        };
        db.AssemblyAttestationEvents.Add(ev);
        await db.SaveChangesAsync(ct);

        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE core.assembly_attestation_events SET \"Statement\" = 'otra' WHERE \"Id\" = {ev.Id}", ct));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.assembly_attestation_events WHERE \"Id\" = {ev.Id}", ct));

        db.AssemblyAttestationEvents.Add(new AssemblyAttestationEventRecord
        {
            AssemblyId = assembly.Id, Kind = "tesoreria", Action = "attest", ActaReference = "x",
            Statement = "Tipo desconocido no permitido.", RecordedBySubject = "qa-ri", CutoffDate = assembly.CutoffDate
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
        Assert.IsType<PostgresException>(ex.InnerException);
    }
}
