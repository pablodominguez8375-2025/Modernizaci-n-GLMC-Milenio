using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Infrastructure;

/// <summary>
/// QA G8 only: deterministic, fake institutional identities bound to the
/// canonical Keycloak issuer. Not a bootstrap or a production account creator.
/// No personal information, passwords or institution-owned identities.
/// </summary>
internal static class QaIdentitySeedService
{
    internal const string CanonicalIssuer = "https://sistema.granlogiamixta.cl/identity/realms/pmgm";

    // Index MUST agree with the Keycloak realm (qa.admin = index 1, no member).
    // OfficeType is an institutional fixture, not an OIDC entitlement.
    private sealed record Fixture(int Index, string Username, string Degree,
                                  string? OfficeType = null, bool ExpiredOffice = false);

    private static readonly Fixture[] Fixtures =
    [
        new(2, "qa.taller23", "master", "secretary"),
        new(3, "qa.venerable23", "master", "venerable_master"),
        new(4, "qa.exvenerable23", "master", "past_master", true),
        new(5, "qa.primer23", "master", "first_warden"),
        new(6, "qa.segundo23", "master", "second_warden"),
        new(7, "qa.orador23", "master", "orator"),
        new(8, "qa.secretaria23", "master", "secretary"),
        new(9, "qa.tesoreria23", "master", "treasurer"),
        new(10, "qa.hospitalaria23", "master", "hospitalarian"),
        new(11, "qa.hermano1", "apprentice"),
        new(12, "qa.hermano2", "fellowcraft"),
        new(13, "qa.hermano3", "master"),
        new(14, "qa.cargovencido23", "master", "secretary", true),
        new(15, "qa.gransecretaria", "master"),
        new(16, "qa.regimeninterior", "master"),
        new(17, "qa.granarchivero", "master"),
        new(18, "qa.bibliotecaeditor", "master"),
        new(19, "qa.bibliotecarevisor", "master"),
        new(20, "qa.bibliotecario", "master"),
        new(21, "qa.comunicaciones", "master")
    ];

    private static Guid Id(string prefix, int index) =>
        Guid.Parse($"{prefix}-0000-4000-8000-{index:000000000000}");

    internal static async Task SeedAsync(PmgmDbContext db, CancellationToken ct)
    {
        var enabled = Environment.GetEnvironmentVariable("PMGM_QA_G8_ENABLED") ?? "false";
        if (enabled == "false") return;
        if (enabled != "true" ||
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Staging" ||
            Environment.GetEnvironmentVariable("PMGM_QA_HTTPS") != "true" ||
            Environment.GetEnvironmentVariable("PMGM_QA_DB2") != "true")
            throw new InvalidOperationException(
                "G8 fixture refused: Staging, canonical HTTPS QA and isolated BD2 required.");

        // The canonical issuer is deliberately NOT inferred from a browser-supplied
        // Host or caller's token. Imported subjects are verified by strict equality.
        if (!await db.Organizations.AsNoTracking().AnyAsync(
                x => x.Id == FirstImplementationSeedService.Lodge23Id, ct))
            throw new InvalidOperationException("G8 fixture requires synthetic Lodge 23.");

        foreach (var fixture in Fixtures)
        {
            var subject = Id("aa820001", fixture.Index).ToString();
            var memberId = Id("bb820002", fixture.Index);
            if (!await db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, ct))
            {
                var person = new Person
                {
                    Id = Id("cc820003", fixture.Index),
                    FirstNames = "Prueba",
                    LastNames = $"G8 {fixture.Index:00}",
                    Email = fixture.Username + "@pmgm.invalid"
                };
                var member = new Member
                {
                    Id = memberId, Person = person,
                    InstitutionalNumber = $"QA-G8-{fixture.Index:00}",
                    CurrentDegree = fixture.Degree
                };
                var membership = new Membership
                {
                    Id = Id("dd820004", fixture.Index), Member = member,
                    OrganizationId = FirstImplementationSeedService.Lodge23Id,
                    MembershipType = "regular",
                    StartDate = new DateOnly(2025, 1, 1),
                    Status = MembershipCodes.MembershipStatus.Active,
                    EvidenceReference = "QA-G8-SYNTHETIC"
                };
                var degreeEvent = new DegreeEvent
                {
                    Id = Id("ee820005", fixture.Index), Member = member,
                    OrganizationId = FirstImplementationSeedService.Lodge23Id,
                    Degree = fixture.Degree,
                    EventType = fixture.Degree switch
                    {
                        "apprentice" => MembershipCodes.DegreeEvent.Initiation,
                        "fellowcraft" => MembershipCodes.DegreeEvent.WageIncrease,
                        _ => MembershipCodes.DegreeEvent.Exaltation
                    },
                    EffectiveDate = new DateOnly(2025, 1, 1),
                    EvidenceReference = "QA-G8-SYNTHETIC"
                };
                db.AddRange(person, member, membership, degreeEvent);
                if (fixture.OfficeType is not null)
                    db.OfficeAssignments.Add(new OfficeAssignment
                    {
                        Id = Id("ff820006", fixture.Index), Member = member,
                        OrganizationId = FirstImplementationSeedService.Lodge23Id,
                        OfficeType = fixture.OfficeType,
                        Period = fixture.ExpiredOffice ? "2025" : "2026",
                        StartDate = fixture.ExpiredOffice
                            ? new DateOnly(2025, 1, 1) : new DateOnly(2026, 1, 1),
                        EndDate = fixture.ExpiredOffice
                            ? new DateOnly(2025, 12, 31) : new DateOnly(2026, 12, 31),
                        EvidenceReference = "QA-G8-SYNTHETIC"
                    });
                await db.SaveChangesAsync(ct);
            }

            var linked = await db.Database.SqlQuery<Guid>($"""
                SELECT "MemberId" AS "Value"
                FROM core.member_identity_links
                WHERE "Issuer" = {CanonicalIssuer}
                  AND "Subject" = {subject}
                  AND "RevokedAtUtc" IS NULL
                """).ToListAsync(ct);
            if (linked.Any(id => id != memberId) || linked.Count > 1)
                throw new InvalidOperationException("G8 fixture conflicts with existing OIDC link.");
            if (linked.Count == 0)
            {
                var linkId = Id("fa820007", fixture.Index);
                var createdAt = DateTimeOffset.UtcNow;
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO core.member_identity_links
                        ("Id", "MemberId", "Issuer", "Subject",
                         "CreatedAtUtc", "CreatedBySubject")
                    VALUES ({linkId}, {memberId}, {CanonicalIssuer},
                            {subject}, {createdAt}, {"qa-g8-fixture"})
                    """, ct);
            }
        }
    }
}
