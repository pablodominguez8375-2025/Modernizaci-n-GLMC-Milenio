using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class InstitutionalBootstrapPostgreSqlTests
{
    [Fact]
    public async Task Bootstrap_creates_GLMC_Libertad23_catalogs_audit_and_is_idempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var coreOptions = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        var bootstrapOptions = new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(connectionString).Options;

        await using var coreDb = new PmgmDbContext(coreOptions);
        await coreDb.Database.MigrateAsync(cancellationToken);
        await using var bootstrapDb = new BootstrapDbContext(bootstrapOptions);
        await bootstrapDb.Database.MigrateAsync(cancellationToken);

        var applied = (await bootstrapDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260909220500_AddInstitutionalBootstrap", applied);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var package = new InstitutionalBootstrapRequest(
            $"glmch-pilot-{suffix}",
            1,
            new BootstrapInstitution("Gran Logia Mixta de Chile"),
            new[] { new BootstrapWorkshop("Respetable Logia Libertad Nº 23", $"23-{suffix}") });

        var service = new InstitutionalBootstrapService(bootstrapDb);
        var plan = await service.PlanAsync(package, cancellationToken);
        Assert.True(plan.Valid);
        Assert.False(plan.AlreadyApplied);
        Assert.Equal(1, plan.Changes.InstitutionsToCreate);
        Assert.Equal(1, plan.Changes.WorkshopsToCreate);
        Assert.Equal(BootstrapCodes.SecurityProfiles.Count, plan.Changes.SecurityProfilesToCreate);
        Assert.Equal(BootstrapCodes.WorkshopOffices.Count, plan.Changes.OfficeDefinitionsToCreate);

        var httpContext = CreateSuperAdminContext($"bootstrap-test-{suffix}");
        var appliedResult = await service.ApplyAsync(httpContext, package, cancellationToken);
        Assert.True(appliedResult.Applied);
        Assert.False(appliedResult.AlreadyApplied);
        Assert.NotNull(appliedResult.Institution);
        Assert.Equal("Gran Logia Mixta de Chile", appliedResult.Institution!.Name);
        Assert.Single(appliedResult.Workshops);
        Assert.Equal("Respetable Logia Libertad Nº 23", appliedResult.Workshops[0].Name);
        Assert.Equal(appliedResult.Institution.Id, appliedResult.Workshops[0].ParentOrganizationId);
        Assert.Contains(appliedResult.Catalog.SecurityProfiles, x => x.Code == InstitutionalRoles.PlatformSuperAdmin);
        Assert.Contains(appliedResult.Catalog.SecurityProfiles, x => x.Code == InstitutionalRoles.GranLogiaAdmin);
        Assert.Contains(appliedResult.Catalog.SecurityProfiles, x => x.Code == InstitutionalRoles.TallerAdmin);
        Assert.Contains(appliedResult.Catalog.OfficeDefinitions, x => x.Code == "worshipful_master" && x.IsPrimary);
        Assert.Contains(appliedResult.Catalog.OfficeDefinitions, x => x.Code == "outer_guard" && !x.IsPrimary);

        var repeated = await service.ApplyAsync(httpContext, package, cancellationToken);
        Assert.True(repeated.Applied);
        Assert.True(repeated.AlreadyApplied);
        Assert.Equal(appliedResult.PayloadSha256, repeated.PayloadSha256);

        var applicationCount = await bootstrapDb.BootstrapApplications.CountAsync(
            x => x.PackageKey == package.PackageKey && x.PackageVersion == 1,
            cancellationToken);
        Assert.Equal(1, applicationCount);

        var auditCount = await bootstrapDb.AuditEvents.CountAsync(
            x => x.Action == "institutional_bootstrap.apply" && x.EntityId == $"{package.PackageKey}:1",
            cancellationToken);
        Assert.Equal(1, auditCount);

        var changedPackage = package with
        {
            Workshops = new[] { new BootstrapWorkshop("Nombre incompatible", $"23-{suffix}") }
        };
        var conflict = await service.PlanAsync(changedPackage, cancellationToken);
        Assert.False(conflict.Valid);
        Assert.Contains(conflict.Errors, x => x.Contains("contenido diferente", StringComparison.OrdinalIgnoreCase));
    }

    private static DefaultHttpContext CreateSuperAdminContext(string subject)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", subject),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin)
        }, "test");

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            TraceIdentifier = $"trace-{subject}"
        };
    }
}
