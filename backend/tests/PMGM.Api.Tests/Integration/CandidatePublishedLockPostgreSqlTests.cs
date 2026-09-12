using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidatePublishedLockPostgreSqlTests
{
    [Fact]
    public async Task Published_candidate_ficha_cannot_be_mutated_after_approval()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var organization = new Organization
        {
            Name = $"Taller QA Insinuados {suffix}",
            Number = $"QA-{suffix}",
            Type = "workshop"
        };
        var person = new Person
        {
            FirstNames = "Persona QA",
            LastNames = suffix
        };
        var ceremony = new CeremonyRequest
        {
            OrganizationId = organization.Id,
            Organization = organization,
            CandidatePersonId = person.Id,
            CandidatePerson = person,
            CeremonyType = CeremonyCodes.Type.Initiation,
            Status = CeremonyCodes.RequestStatus.UnderReview,
            ProposedDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30))
        };
        var publication = new CandidatePublication
        {
            CeremonyRequestId = ceremony.Id,
            CeremonyRequest = ceremony,
            PersonId = person.Id,
            Person = person,
            OrganizationId = organization.Id,
            Organization = organization,
            PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(-2),
            RequiredDays = 20,
            RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            Status = CeremonyCodes.PublicationStatus.Published
        };

        db.AddRange(organization, person, ceremony, publication);
        await db.SaveChangesAsync(cancellationToken);

        var nextCalled = false;
        var middleware = new CandidatePublishedLockMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.Path = $"/api/insinuados/solicitudes/{ceremony.Id}/ficha";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Unpublished_candidate_ficha_reaches_endpoint_pipeline()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var requestId = Guid.NewGuid();
        var nextCalled = false;
        var middleware = new CandidatePublishedLockMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.Path = $"/api/insinuados/solicitudes/{requestId}/ficha";

        await middleware.InvokeAsync(context, db);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }
}
