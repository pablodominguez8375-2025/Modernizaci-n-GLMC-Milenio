using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Tests.Unit;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberPhotoPostgreSqlTests
{
    private const string Issuer = "urn:pmgm:unspecified-issuer";
    private const string Subject = "ci-http-admin";

    [Fact]
    public async Task Member_manages_own_photo_and_others_see_it_only_when_visible()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        Guid ownId, hiddenId, visibleId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM core.member_identity_links WHERE \"Issuer\" = {Issuer} AND \"Subject\" = {Subject}", ct);
            Member NewMember(string tag)
            {
                var person = new Person { FirstNames = "Foto", LastNames = tag, Rut = "QA-" + Guid.NewGuid().ToString("N")[..12] };
                var member = new Member { Person = person, PersonId = person.Id, InstitutionalNumber = $"PH-{Guid.NewGuid():N}"[..20] };
                db.Add(member);
                return member;
            }
            var own = NewMember("Propia"); var hidden = NewMember("Oculta"); var visible = NewMember("Visible");
            var org = new Organization { Name = $"Taller Foto {Guid.NewGuid():N}"[..40], Number = Guid.NewGuid().ToString("N")[..8], Type = "workshop" };
            db.Add(new Membership { Member = own, MemberId = own.Id, Organization = org, OrganizationId = org.Id, MembershipType = "regular", StartDate = new DateOnly(2020, 1, 10), Status = MembershipCodes.MembershipStatus.Active });
            db.Add(new DegreeEvent { Member = own, MemberId = own.Id, Organization = org, OrganizationId = org.Id, Degree = "1", EventType = MembershipCodes.DegreeEvent.Initiation, EffectiveDate = new DateOnly(2020, 1, 10) });
            var jpeg = MemberPhotoPolicyTests.Jpeg(300, withExif: false);
            db.AddRange(
                new MemberPhoto { MemberId = hidden.Id, Content = jpeg, ContentType = "image/jpeg", Sha256 = "h1", Width = 300, Height = 300, VisibleToMembers = false },
                new MemberPhoto { MemberId = visible.Id, Content = jpeg, ContentType = "image/jpeg", Sha256 = "v1", Width = 300, Height = 300, VisibleToMembers = true });
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                VALUES ({Guid.NewGuid()}, {own.Id}, {Issuer}, {Subject}, {DateTimeOffset.UtcNow}, {Subject}, NULL)
                """, ct);
            ownId = own.Id; hiddenId = hidden.Id; visibleId = visible.Id;
        }

        // Sin foto: estado vacío y sin contenido.
        var status = await (await client.GetAsync("/api/member-self/photo", ct)).Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.False(status.GetProperty("hasPhoto").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/member-self/photo/content", ct)).StatusCode);

        // Rechazos: no cuadrada, formato falso, demasiado pesada.
        using (var wide = Upload(MemberPhotoPolicyTests.Jpeg(300, height: 200))) Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/member-self/photo", wide, ct)).StatusCode);
        using (var fake = Upload([1, 2, 3, 4, 5, 6, 7, 8, 9, 10])) Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/member-self/photo", fake, ct)).StatusCode);
        using (var big = Upload(MemberPhotoPolicyTests.Jpeg(300, padding: MemberPhotoPolicy.MaxBytes))) Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/member-self/photo", big, ct)).StatusCode);

        // Alta válida con EXIF: se guarda limpia y NO visible.
        using (var good = Upload(MemberPhotoPolicyTests.Jpeg(300, withExif: true)))
        {
            using var response = await client.PutAsync("/api/member-self/photo", good, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.False(json.GetProperty("visibleToMembers").GetBoolean());
        }
        var stored = await (await client.GetAsync("/api/member-self/photo/content", ct)).Content.ReadAsByteArrayAsync(ct);
        Assert.DoesNotContain("Exif", System.Text.Encoding.ASCII.GetString(stored), StringComparison.Ordinal);

        // Visibilidad: la decide el titular; recién entonces otros la ven (lo ve cualquier Hermano autenticado).
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/member-self/photo/visibility", new { visible = true }, ct)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.True(await db.MemberPhotos.AnyAsync(x => x.MemberId == ownId && x.VisibleToMembers, ct));
        }
        using var visibleResponse = await client.GetAsync($"/api/member-photos/{visibleId}", ct);
        Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);
        Assert.Equal("image/jpeg", visibleResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-cache", visibleResponse.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/member-photos/{hiddenId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/member-photos/{Guid.NewGuid()}", ct)).StatusCode);

        // Retirar: vuelve al avatar con iniciales y queda auditado sin guardar la imagen.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/member-self/photo", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/member-photos/{ownId}", ct)).StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var actions = await check.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents
            .Where(x => x.EntityId == ownId.ToString() && x.Action.StartsWith("member.photo.")).Select(x => x.Action).ToListAsync(ct);
        Assert.Contains("member.photo.added", actions);
        Assert.Contains("member.photo.visibility_changed", actions);
        Assert.Contains("member.photo.removed", actions);
    }

    private static ByteArrayContent Upload(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        return content;
    }
}
