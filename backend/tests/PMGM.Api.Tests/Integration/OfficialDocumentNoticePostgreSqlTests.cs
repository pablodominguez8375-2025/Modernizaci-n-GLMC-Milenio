using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.GrandArchive;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Notifications;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class OfficialDocumentNoticePostgreSqlTests
{
    [Fact]
    public async Task Publication_notice_reaches_only_brothers_whose_degree_allows_the_document_and_never_twice()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        var coreOptions = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        var notifOptions = new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connectionString).Options;
        await using var coreDb = new PmgmDbContext(coreOptions);
        await coreDb.Database.MigrateAsync(ct);
        await using var notifDb = new NotificationDbContext(notifOptions);
        await notifDb.Database.MigrateAsync(ct);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var org = new Organization { Name = $"Taller Avisos {suffix}", Number = $"V{suffix[..3]}", Type = "workshop" };
        coreDb.Organizations.Add(org);
        var people = new Dictionary<string, (Member Member, string Subject)>();
        foreach (var degree in new[] { "1", "2", "3" })
        {
            var person = new Person { FirstNames = $"H{degree}", LastNames = $"Aviso {suffix}" };
            var member = new Member { Person = person, PersonId = person.Id };
            coreDb.AddRange(person, member, new DegreeEvent
            {
                Member = member, MemberId = member.Id, Organization = org, OrganizationId = org.Id,
                Degree = degree, EventType = MembershipCodes.DegreeEvent.Exaltation, EffectiveDate = new DateOnly(2020, 1, 1)
            });
            people[degree] = (member, $"notice-{suffix}-{degree}");
        }
        // Un Hermano de grado 3 con la cuenta revocada no recibe nada.
        var revokedPerson = new Person { FirstNames = "Revocado", LastNames = $"Aviso {suffix}" };
        var revokedMember = new Member { Person = revokedPerson, PersonId = revokedPerson.Id };
        coreDb.AddRange(revokedPerson, revokedMember, new DegreeEvent
        {
            Member = revokedMember, MemberId = revokedMember.Id, Organization = org, OrganizationId = org.Id,
            Degree = "3", EventType = MembershipCodes.DegreeEvent.Exaltation, EffectiveDate = new DateOnly(2020, 1, 1)
        });
        await coreDb.SaveChangesAsync(ct);
        foreach (var (member, subject) in people.Values)
            await coreDb.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links ("Id","MemberId","Issuer","Subject","CreatedAtUtc","CreatedBySubject","RevokedAtUtc")
                VALUES ({Guid.NewGuid()}, {member.Id}, {"qa-issuer"}, {subject}, {DateTimeOffset.UtcNow}, {"qa"}, NULL)
                """, ct);
        await coreDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO core.member_identity_links ("Id","MemberId","Issuer","Subject","CreatedAtUtc","CreatedBySubject","RevokedAtUtc")
            VALUES ({Guid.NewGuid()}, {revokedMember.Id}, {"qa-issuer"}, {$"notice-{suffix}-revoked"}, {DateTimeOffset.UtcNow}, {"qa"}, {DateTimeOffset.UtcNow})
            """, ct);

        var service = new OfficialDocumentNoticeService(coreDb, new InstitutionalNotificationService(notifDb));
        var recordId = Guid.NewGuid();
        var cycle = DateTimeOffset.UtcNow;
        async Task<List<string>> Recipients() => await notifDb.NotificationMessages.AsNoTracking()
            .Where(x => x.RelatedResourceId == recordId.ToString("N")).Select(x => x.RecipientSubject).ToListAsync(ct);
        bool Mine(string subject) => subject.StartsWith($"notice-{suffix}-", StringComparison.Ordinal);

        // Solo Maestros: avisa únicamente al de grado 3 con cuenta activa.
        var master = await service.NotifyPublishedAsync(recordId, "DEC-QA", GrandArchiveCodes.RecordType.Decree, "Decreto QA", 3, cycle, ct);
        Assert.Equal(0, master.Failed);
        Assert.Equal([$"notice-{suffix}-3"], (await Recipients()).Where(Mine).ToArray());

        // Repetir la publicación no duplica avisos.
        await service.NotifyPublishedAsync(recordId, "DEC-QA", GrandArchiveCodes.RecordType.Decree, "Decreto QA", 3, cycle, ct);
        Assert.Single(await Recipients(), Mine);

        // Ampliar a Compañeros avisa solo al grado 2 (el Maestro ya fue avisado).
        await service.NotifyPublishedAsync(recordId, "DEC-QA", GrandArchiveCodes.RecordType.Decree, "Decreto QA", 2, cycle, ct);
        Assert.Equal(new[] { $"notice-{suffix}-2", $"notice-{suffix}-3" }, (await Recipients()).Where(Mine).OrderBy(x => x).ToArray());

        // Ampliar a todos suma al Aprendiz; el aviso es interno y no exige acción.
        await service.NotifyPublishedAsync(recordId, "DEC-QA", GrandArchiveCodes.RecordType.Decree, "Decreto QA", 1, cycle, ct);
        Assert.Equal(3, (await Recipients()).Count(Mine));
        var sample = await notifDb.NotificationMessages.AsNoTracking().Include(x => x.Deliveries).FirstAsync(x => x.RelatedResourceId == recordId.ToString("N") && x.RecipientSubject.StartsWith($"notice-{suffix}-"), ct);
        Assert.Equal("/official-documents", sample.ActionUrl);
        Assert.False(sample.ActionRequired);
        Assert.Contains("Decreto QA", sample.Body);
        Assert.All(sample.Deliveries, d => Assert.Equal(NotificationCodes.Channel.Internal, d.Channel));

        // Un ciclo nuevo (publicado de nuevo tras vencer) vuelve a avisar.
        await service.NotifyPublishedAsync(recordId, "DEC-QA", GrandArchiveCodes.RecordType.Decree, "Decreto QA", 1, cycle.AddDays(400), ct);
        Assert.Equal(6, (await Recipients()).Count(Mine));
    }
}
