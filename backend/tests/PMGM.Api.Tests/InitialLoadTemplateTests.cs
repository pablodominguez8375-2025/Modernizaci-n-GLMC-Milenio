using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadTemplateTests
{
    [Fact]
    public void Has_three_exact_areas_and_no_extra_or_personal_data()
    {
        Assert.Equal(new[] { "Talleres.csv", "Hermanos.csv", "CargosVigentes.csv" },
            InitialLoadTemplateEndpoints.Sheets.Select(s => s.Name));
        Assert.Equal(new[]
            {
                "personRef", "identityType", "identityNumber", "firstNames", "lastNames",
                "email", "workshopCode", "grade", "institutionalStatus"
            },
            InitialLoadTemplateEndpoints.Sheets[1].Columns);
        Assert.Equal(new[] { "personRef", "workshopCode", "officeCode", "startDate" },
            InitialLoadTemplateEndpoints.Sheets[2].Columns);
        Assert.DoesNotContain(InitialLoadTemplateEndpoints.Sheets,
            sheet => sheet.Columns.Any(column => column.StartsWith("password", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Csv_archive_contains_header_only_no_demo_or_real_records()
    {
        var archive = InitialLoadTemplateEndpoints.CreateEmptyCsvArchive();
        Assert.InRange(archive.Length, 1, 8192);
        using var stream = new MemoryStream(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(3, zip.Entries.Count);
        for (var i = 0; i < zip.Entries.Count; i++)
        {
            var template = InitialLoadTemplateEndpoints.Sheets[i];
            var entry = zip.Entries[i];
            Assert.Equal(template.Name, entry.FullName);
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            Assert.Equal(string.Join(",", template.Columns), reader.ReadLine());
            Assert.Null(reader.ReadLine()); // No names, RUTs, emails, offices or example rows.
        }
    }

    [Fact]
    public void Only_master_with_order_scope_is_authorized_by_platform_policy()
    {
        var access = new InstitutionalAccessService();
        ClaimsPrincipal Subject(string role, bool order) =>
            new(new ClaimsIdentity(
                (order
                    ? new[] { new Claim(InstitutionalClaims.Scope, "order") }
                    : Array.Empty<Claim>()).Concat(
                        [new Claim(InstitutionalClaims.Role, role)]), "test"));

        Assert.True(access.IsPlatformSuperAdmin(Subject(InstitutionalRoles.PlatformSuperAdmin, true)));
        Assert.False(access.IsPlatformSuperAdmin(Subject(InstitutionalRoles.PlatformSuperAdmin, false)));
        Assert.False(access.IsPlatformSuperAdmin(Subject(InstitutionalRoles.GranLogiaAdmin, true)));
        Assert.False(access.IsPlatformSuperAdmin(Subject(InstitutionalRoles.TallerSecretaria, true)));
    }
}
