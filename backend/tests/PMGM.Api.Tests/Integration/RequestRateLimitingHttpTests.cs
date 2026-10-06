using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Infrastructure;
using Xunit;

namespace PMGM.Api.Tests.Integration;

public sealed class RequestRateLimitingHttpTests
{
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RequestRateLimiting:WritesPerMinute"] = "2",
            ["RequestRateLimiting:UploadsPerMinute"] = "1",
            ["RequestRateLimiting:ExportsPerMinute"] = "1"
        });
        builder.Services.AddAuthentication("RateTest")
            .AddScheme<AuthenticationSchemeOptions, RateTestAuthenticationHandler>("RateTest", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddInstitutionalRequestRateLimiting(builder.Configuration);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapPost("/api/writes/{id}", () => Results.Ok()).RequireAuthorization();
        app.MapPut("/api/documentos/versiones/{id}/contenido", () => Results.Ok()).RequireAuthorization();
        app.MapGet("/api/documentos/versiones/{id}/contenido", () => Results.Ok()).RequireAuthorization();
        app.MapGet("/api/calendar/ics", () => Results.Ok()).RequireAuthorization();
        app.MapGet("/api/read", () => Results.Ok()).RequireAuthorization();
        app.MapGet("/health/live", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient Client(WebApplication app, string? subject = "actor-a", string issuer = "issuer-a")
    {
        var client = app.GetTestClient();
        if (subject is not null) client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Issuer", issuer);
        return client;
    }

    [Fact]
    public async Task Writes_share_budget_across_paths_and_queries_and_return_retry_after()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/writes/two?organizationId=other", null, TestContext.Current.CancellationToken)).StatusCode);
        var rejected = await client.PostAsync("/api/writes/three", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Contains("Espera un momento", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(rejected.Headers.CacheControl?.NoStore == true);
    }

    [Fact]
    public async Task Subjects_and_issuers_have_independent_budgets_even_on_same_peer()
    {
        await using var app = await StartAsync();
        using var first = Client(app);
        await first.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken);
        await first.PostAsync("/api/writes/two", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.PostAsync("/api/writes/three", null, TestContext.Current.CancellationToken)).StatusCode);
        using var second = Client(app, "actor-b");
        using var otherIssuer = Client(app, "actor-a", "issuer-b");
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otherIssuer.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Upload_and_export_budgets_are_separate_from_writes_and_cover_all_documents()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync("/api/documentos/versiones/one/contenido", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PutAsync("/api/documentos/versiones/two/contenido", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PutAsync("/api/documentos/versiones/two/contenido/", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documentos/versiones/one/contenido", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/calendar/ics", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/calendar/ics/", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_writes_cannot_exceed_budget()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(index => client.PostAsync($"/api/writes/{index}", null, TestContext.Current.CancellationToken)));
        Assert.Equal(2, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(6, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Ordinary_reads_and_health_remain_available_after_exhaustion()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        await client.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken);
        await client.PostAsync("/api/writes/two", null, TestContext.Current.CancellationToken);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/read", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        }
    }

    [Fact]
    public async Task Anonymous_requests_are_still_unauthorized_and_do_not_consume_user_budget()
    {
        await using var app = await StartAsync();
        using var anonymous = Client(app, null);
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken)).StatusCode);
        using var authenticated = Client(app);
        Assert.Equal(HttpStatusCode.OK, (await authenticated.PostAsync("/api/writes/one", null, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public void Invalid_limits_fail_startup_instead_of_disabling_protection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["RequestRateLimiting:WritesPerMinute"] = "0" }).Build();
        Assert.Throws<InvalidOperationException>(() => services.AddInstitutionalRequestRateLimiting(configuration));
    }
}

// Synthetic authentication only for this isolated HTTP middleware harness.
internal sealed class RateTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Request.Headers["X-Test-Subject"].ToString();
        if (string.IsNullOrEmpty(subject)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim("sub", subject, ClaimValueTypes.String,
            Request.Headers["X-Test-Issuer"].ToString())], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
