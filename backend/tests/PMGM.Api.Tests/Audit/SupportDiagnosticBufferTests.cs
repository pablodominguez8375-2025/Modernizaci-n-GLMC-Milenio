using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using PMGM.Api.Modules.Audit;
using Xunit;

namespace PMGM.Api.Tests.Audit;

public sealed class SupportDiagnosticBufferTests
{
    [Fact]
    public void StoresOnlyErrorsAndWarningsWithBoundedRetention()
    {
        var store = new SupportDiagnosticBuffer();
        var clock = new DateTimeOffset(2026, 10, 23, 22, 30, 0, TimeSpan.Zero);
        store.Record(new SupportDiagnosticEntry(clock, "", 200, "GET", "/health/ready", "test1"));
        for (var i = 0; i < 510; i++)
        {
            store.Record(new SupportDiagnosticEntry(clock.AddSeconds(i), "", i % 2 == 0 ? 500 : 403,
                "GET", "/api/gestion-logial/talleres/{organizationId:guid}/tenidas", $"qa-{i}"));
        }

        var result = store.Snapshot();
        Assert.Equal(SupportDiagnosticBuffer.Capacity, result.Length);
        Assert.Equal("qa-509", result[0].CorrelationId);
        Assert.Equal("qa-10", result[^1].CorrelationId);
        Assert.DoesNotContain(result, item => item.StatusCode == 200);
        Assert.All(result, item => Assert.Contains(item.Severity, new[] { "error", "warning" }));
    }

    [Fact]
    public void StoresOnlySanitizedMetadataAndRejectsUntrustedCorrelationId()
    {
        var store = new SupportDiagnosticBuffer();
        store.Record(new SupportDiagnosticEntry(DateTimeOffset.UtcNow, "other",
            500, "UNUSUAL", "/api/test/{id:guid}", "Authorization Bearer secret"));

        var item = Assert.Single(store.Snapshot());
        Assert.Equal("error", item.Severity);
        Assert.Equal("OTHER", item.Method);
        Assert.Equal("(no disponible)", item.CorrelationId);
        Assert.Equal("/api/test/{id:guid}", item.Route);
    }

    [Fact]
    public async Task MiddlewareKeepsOnlyRouteTemplateNeverUserPathOrQuery()
    {
        var store = new SupportDiagnosticBuffer();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/gestion-logial/talleres/23232323-2323-2323-2323-232323232323/tenidas";
        context.Request.QueryString = new QueryString("?token=extremely-secret");
        context.TraceIdentifier = "qa-http-500";
        var route = RoutePatternFactory.Parse("/api/gestion-logial/talleres/{organizationId:guid}/tenidas");
        context.SetEndpoint(new RouteEndpointBuilder(
            _ => Task.CompletedTask, route, 0).Build());

        var middleware = new SupportDiagnosticMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, store);

        var entry = Assert.Single(store.Snapshot());
        Assert.Equal("/api/gestion-logial/talleres/{organizationId:guid}/tenidas", entry.Route);
        Assert.DoesNotContain("23232323", entry.Route);
        Assert.DoesNotContain("secret", entry.Route);
        Assert.Equal("qa-http-500", entry.CorrelationId);
    }

    [Fact]
    public void SnapshotIsAStableCopyForConcurrentReadAndWrite()
    {
        var store = new SupportDiagnosticBuffer();
        Parallel.For(0, 2000, index =>
        {
            store.Record(new SupportDiagnosticEntry(DateTimeOffset.UtcNow,
                "", 500, "GET", "/api/qa/{id:guid}", $"qa-{index}"));
            _ = store.Snapshot();
        });
        Assert.Equal(SupportDiagnosticBuffer.Capacity, store.Snapshot().Length);
    }
}
