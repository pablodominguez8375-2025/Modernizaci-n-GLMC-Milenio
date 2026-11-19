using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.ExecutiveReporting;
using PMGM.Api.Modules.SystemConfiguration;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class ExecutiveReportingPostgreSqlTests
{
    [Fact]
    public async Task Report_preserves_degree_and_past_active_after_workshop_transfer()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var mainOptions = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var lodgeOptions = new DbContextOptionsBuilder<LodgeManagementDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(mainOptions);
        await using var lodgeDb = new LodgeManagementDbContext(lodgeOptions);
        await db.Database.MigrateAsync(cancellationToken);
        await lodgeDb.Database.MigrateAsync(cancellationToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var source = new Organization { Name = $"Taller Origen {suffix}", Number = $"9{suffix[..2]}", Type = "workshop" };
        var target = new Organization { Name = $"Taller Destino {suffix}", Number = $"8{suffix[..2]}", Type = "workshop" };
        var person = new Person { FirstNames = "Hermano", LastNames = $"Trasladado {suffix}" };
        var member = new Member { Person = person, InstitutionalNumber = $"QA-{suffix}" };
        var sourceMembership = new Membership
        {
            Member = member,
            Organization = source,
            MembershipType = "regular",
            StartDate = new DateOnly(2018, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            Status = MembershipCodes.MembershipStatus.Transferred,
            EndReason = "Traslado QA"
        };
        var targetMembership = new Membership
        {
            Member = member,
            Organization = target,
            MembershipType = "regular",
            StartDate = new DateOnly(2026, 1, 1),
            Status = MembershipCodes.MembershipStatus.Active
        };

        db.AddRange(source, target, person, member, sourceMembership, targetMembership);
        db.DegreeEvents.Add(new DegreeEvent
        {
            Member = member,
            Organization = source,
            Degree = "master",
            EventType = MembershipCodes.DegreeEvent.Exaltation,
            EffectiveDate = new DateOnly(2022, 6, 15)
        });
        db.OfficeAssignments.Add(new OfficeAssignment
        {
            Member = member,
            Organization = source,
            OfficeType = "worshipful_master",
            Period = "2023",
            StartDate = new DateOnly(2023, 1, 1),
            EndDate = new DateOnly(2023, 12, 31)
        });
        db.MemberTransfers.Add(new MemberTransfer
        {
            Member = member,
            SourceMembership = sourceMembership,
            SourceOrganization = source,
            TargetOrganization = target,
            TargetMembership = targetMembership,
            RequestedDate = new DateOnly(2025, 11, 1),
            ProposedEffectiveDate = new DateOnly(2026, 1, 1),
            ApprovedEffectiveDate = new DateOnly(2026, 1, 1),
            Status = MembershipCodes.TransferStatus.Executed,
            ExecutedAtUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
        });
        db.FinancialRegularitySnapshots.AddRange(
            new FinancialRegularitySnapshot
            {
                Organization = target,
                Member = member,
                Scope = "member",
                Status = "delinquent",
                AsOfDate = new DateOnly(2026, 9, 1)
            },
            new FinancialRegularitySnapshot
            {
                Organization = target,
                MemberId = null,
                Scope = "organization",
                Status = "delinquent",
                AsOfDate = new DateOnly(2026, 9, 1)
            });
        db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot
        {
            Organization = target,
            Status = "overdue",
            AsOfDate = new DateOnly(2026, 9, 1)
        });
        await db.SaveChangesAsync(cancellationToken);

        lodgeDb.LodgeMeetings.Add(new LodgeMeeting
        {
            OrganizationId = target.Id,
            MeetingDate = new DateOnly(2026, 8, 20),
            MeetingType = "regular",
            Grade = "master",
            Status = "closed"
        });
        lodgeDb.LodgeInstructionSessions.Add(new LodgeInstructionSession
        {
            OrganizationId = target.Id,
            InstructionDate = new DateOnly(2026, 8, 22),
            Grade = "master",
            Topic = "Docencia QA",
            ResponsibleOffice = "first_warden",
            Status = "closed",
            CreatedBySubject = "qa"
        });
        await lodgeDb.SaveChangesAsync(cancellationToken);

        var service = new ExecutiveReportingService(db, lodgeDb);
        var report = await service.BuildAsync(
            new DateOnly(2026, 9, 9),
            new DateOnly(2026, 1, 1),
            cancellationToken);

        var targetRow = Assert.Single(report.Workshops, x => x.OrganizationId == target.Id);
        Assert.Equal(1, targetRow.CurrentMembers);
        Assert.Equal(1, targetRow.ActiveMembers);
        Assert.Equal(1, targetRow.PastActive);
        Assert.Equal(1, targetRow.DegreeDistribution["master"]);
        Assert.Equal(1, targetRow.Financial.DelinquentAffiliations);
        Assert.Equal("delinquent", targetRow.Financial.WorkshopTreasuryStatus);
        Assert.Equal("overdue", targetRow.Financial.WorkshopHospitalariaStatus);
        Assert.Equal(1, targetRow.Activity.Meetings);
        Assert.Equal(1, targetRow.Activity.InstructionSessions);
        Assert.True(targetRow.AttentionRequired);
        Assert.Contains("treasury_delinquent", targetRow.AttentionFlags);
        Assert.Contains("hospitalaria_overdue", targetRow.AttentionFlags);
    }

    [Fact]
    public async Task Retired_workshop_keeps_historical_row_but_leaves_operational_metrics()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options);
        await using var lodgeDb = new LodgeManagementDbContext(new DbContextOptionsBuilder<LodgeManagementDbContext>().UseNpgsql(connectionString).Options);
        await db.Database.MigrateAsync(ct); await lodgeDb.Database.MigrateAsync(ct);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workshop = new Organization { Name = $"Taller Baja {suffix}", Number = $"7{suffix[..2]}", Type = "workshop" };
        var person = new Person { FirstNames = "Hermano", LastNames = $"De Baja {suffix}" };
        var member = new Member { Person = person, InstitutionalNumber = $"QB-{suffix}" };
        db.AddRange(workshop, person, member);
        db.Memberships.Add(new Membership { Member = member, Organization = workshop, MembershipType = "regular", StartDate = new DateOnly(2020, 1, 1), Status = MembershipCodes.MembershipStatus.Active });
        db.FinancialRegularitySnapshots.Add(new FinancialRegularitySnapshot { Organization = workshop, MemberId = null, Scope = "organization", Status = "delinquent", AsOfDate = new DateOnly(2026, 9, 1) });
        await db.SaveChangesAsync(ct);

        var service = new ExecutiveReportingService(db, lodgeDb);
        var asOf = new DateOnly(2026, 9, 9); var from = new DateOnly(2026, 1, 1);
        var before = await service.BuildAsync(asOf, from, ct);
        Assert.Equal("active", Assert.Single(before.Workshops, x => x.OrganizationId == workshop.Id).Status);
        Assert.True(Assert.Single(before.Workshops, x => x.OrganizationId == workshop.Id).AttentionRequired);

        try
        {
            workshop.Status = "retired"; workshop.RetiredAtUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero); workshop.RetiredReason = "prueba T2b-2";
            // Baseline autorizado (backfill): sin historial previo la fecha de corte anterior sería «desconocida» (fail-closed).
            db.WorkshopStatusTransitions.Add(new WorkshopStatusTransition { SubjectRef = workshop.Id, PreviousStatus = "active", NextStatus = "retired", EffectiveAtUtc = workshop.RetiredAtUtc.Value, Origin = WorkshopTransitionOrigins.Backfill });
            await db.SaveChangesAsync(ct);
            var after = await service.BuildAsync(asOf, from, ct);
            var row = Assert.Single(after.Workshops, x => x.OrganizationId == workshop.Id);
            Assert.Equal("retired", row.Status); Assert.NotNull(row.RetiredAtUtc);
            Assert.Equal(1, row.CurrentMembers); Assert.False(row.AttentionRequired); Assert.Empty(row.AttentionFlags);
            Assert.Equal(before.Overview.Workshops - 1, after.Overview.Workshops);
            Assert.Equal(before.Overview.CurrentMembers - 1, after.Overview.CurrentMembers);
            Assert.Equal(before.Overview.Financial.WorkshopsTreasuryDelinquent - 1, after.Overview.Financial.WorkshopsTreasuryDelinquent);
            Assert.Equal(before.Overview.WorkshopsRequiringAttention - 1, after.Overview.WorkshopsRequiringAttention);
            Assert.Equal(before.Overview.RetiredWorkshops + 1, after.Overview.RetiredWorkshops);

            // Un corte ANTERIOR a la baja no reescribe la historia: el Taller sigue activo en esa fecha.
            var earlier = await service.BuildAsync(new DateOnly(2026, 8, 15), from, ct);
            Assert.Equal("active", Assert.Single(earlier.Workshops, x => x.OrganizationId == workshop.Id).Status);
        }
        finally
        {
            // La base de CI es compartida entre pruebas: reactivar para no dejar un Taller de baja a otras pruebas.
            workshop.Status = "active"; workshop.RetiredAtUtc = null; workshop.RetiredReason = null; await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Transition_history_reconstructs_cycles_and_is_append_only()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options);
        await using var lodgeDb = new LodgeManagementDbContext(new DbContextOptionsBuilder<LodgeManagementDbContext>().UseNpgsql(connectionString).Options);
        await db.Database.MigrateAsync(ct); await lodgeDb.Database.MigrateAsync(ct);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workshop = new Organization { Name = $"Taller Ciclos {suffix}", Number = $"6{suffix[..2]}", Type = "workshop" };
        db.Organizations.Add(workshop); await db.SaveChangesAsync(ct);
        DateTimeOffset D(int m, int d) => new(2026, m, d, 12, 0, 0, TimeSpan.Zero);
        // Una transición por SaveChanges: la identidad `Sequence` debe seguir el orden real (en producción cada baja/reactivación es su propia transacción).
        foreach (var (from, to, at) in new[] { ("active", "retired", D(3, 1)), ("retired", "active", D(6, 1)), ("active", "retired", D(9, 1)) })
        {
            db.WorkshopStatusTransitions.Add(new WorkshopStatusTransition { SubjectRef = workshop.Id, PreviousStatus = from, NextStatus = to, EffectiveAtUtc = at, Origin = WorkshopTransitionOrigins.Backfill });
            await db.SaveChangesAsync(ct);
        }
        // La fila actual refleja la última transición.
        workshop.Status = "retired"; workshop.RetiredAtUtc = D(9, 1); workshop.RetiredReason = "prueba ciclos";
        await db.SaveChangesAsync(ct);
        try
        {
            var service = new ExecutiveReportingService(db, lodgeDb);
            async Task<string> StatusAt(int m, int d) => Assert.Single((await service.BuildAsync(new DateOnly(2026, m, d), new DateOnly(2026, 1, 1), ct)).Workshops, x => x.OrganizationId == workshop.Id).Status;
            Assert.Equal("active", await StatusAt(2, 28));
            Assert.Equal("retired", await StatusAt(4, 1));
            Assert.Equal("active", await StatusAt(7, 1));
            Assert.Equal("retired", await StatusAt(9, 9));

            // Append-only a nivel de BD (migración manual AppendOnly aplicada por MigrateAsync en la BD de prueba).
            var first = await db.WorkshopStatusTransitions.AsNoTracking().OrderBy(x => x.Sequence).FirstAsync(x => x.SubjectRef == workshop.Id, ct);
            var update = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE core.workshop_status_transitions SET \"NextStatus\"='active' WHERE \"Id\"={0}", [first.Id], ct));
            Assert.Contains("PM410", update.ToString());
            var delete = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM core.workshop_status_transitions WHERE \"Id\"={0}", [first.Id], ct));
            Assert.Contains("PM410", delete.ToString());
            var truncate = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE core.workshop_status_transitions", ct));
            Assert.Contains("PM410", truncate.ToString());

            // Integridad de INSERT (trigger): sujeto inexistente / no Taller, cadena rota, fecha retroactiva, actor obligatorio.
            async Task<string> Insert(Guid subject, string from, string to, DateTimeOffset at, string origin = "runtime", string? actor = "sub", string? reason = "trace")
            {
                var e = await Assert.ThrowsAnyAsync<Exception>(async () =>
                {
                    await using var other = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options);
                    other.WorkshopStatusTransitions.Add(new WorkshopStatusTransition { SubjectRef = subject, PreviousStatus = from, NextStatus = to, EffectiveAtUtc = at, Origin = origin, ActorSubject = actor, ReasonRef = reason });
                    await other.SaveChangesAsync(ct);
                });
                return e.ToString();
            }
            Assert.Contains("PM411", await Insert(Guid.NewGuid(), "active", "retired", DateTimeOffset.UtcNow));
            var support = new Organization { Name = $"Gran Logia {suffix}", Type = "grand_lodge" };
            db.Organizations.Add(support); await db.SaveChangesAsync(ct);
            Assert.Contains("PM411", await Insert(support.Id, "active", "retired", DateTimeOffset.UtcNow));
            Assert.Contains("PM412", await Insert(workshop.Id, "active", "retired", DateTimeOffset.UtcNow));          // la última quedó en retired
            Assert.Contains("PM413", await Insert(workshop.Id, "retired", "active", D(1, 1)));                          // retroactiva en línea
            Assert.Contains("23514", await Insert(workshop.Id, "retired", "active", DateTimeOffset.UtcNow, actor: null)); // actor obligatorio en runtime

            // Una transición con fecha NO confiable no participa en series ni inventa fechas.
            db.WorkshopStatusTransitions.Add(new WorkshopStatusTransition { SubjectRef = workshop.Id, PreviousStatus = "retired", NextStatus = "active", EffectiveAtUtc = D(9, 2), Origin = WorkshopTransitionOrigins.Backfill, DateKnown = false });
            await db.SaveChangesAsync(ct);
            Assert.Equal("retired", await StatusAt(12, 31));

            // Reversión de transacción: no deja filas.
            var before = await db.WorkshopStatusTransitions.CountAsync(x => x.SubjectRef == workshop.Id, ct);
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                db.WorkshopStatusTransitions.Add(new WorkshopStatusTransition { SubjectRef = workshop.Id, PreviousStatus = "active", NextStatus = "retired", EffectiveAtUtc = DateTimeOffset.UtcNow, ActorSubject = "sub", ReasonRef = "trace" });
                await db.SaveChangesAsync(ct); await tx.RollbackAsync(ct);
            }
            db.ChangeTracker.Clear();
            Assert.Equal(before, await db.WorkshopStatusTransitions.CountAsync(x => x.SubjectRef == workshop.Id, ct));
        }
        finally
        {
            await db.Organizations.Where(x => x.Id == workshop.Id).ExecuteUpdateAsync(x => x.SetProperty(o => o.Status, "active").SetProperty(o => o.RetiredAtUtc, (DateTimeOffset?)null).SetProperty(o => o.RetiredReason, (string?)null), CancellationToken.None);
        }
    }
}
