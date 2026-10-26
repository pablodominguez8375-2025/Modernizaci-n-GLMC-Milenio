using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using PMGM.Api.Modules.Audit;
using Xunit;

namespace PMGM.Api.Tests;

/// <summary>
/// #681: system support diagnostics must retain only the fixed route template,
/// even if the exception handler erases endpoint selection after routing.
/// </summary>
public sealed class SupportDiagnosticRouteTests
{
    private const string Route = "/api/gestion-logial/talleres/{organizationId:guid}/tenidas";
    private const string ActualPath =
        "/api/gestion-logial/talleres/11111111-1111-4111-8111-111111111111/tenidas";

    private static DefaultHttpContext Context(bool routed)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = ActualPath;
        context.TraceIdentifier = "synthetic-correlation-681";
        if (routed)
        {
            var builder = new RouteEndpointBuilder(
                _ => Task.CompletedTask, RoutePatternFactory.Parse(Route), 0);
            context.SetEndpoint(builder.Build());
        }
        return context;
    }

    [Fact]
    public async Task Handler_can_clear_endpoint_without_losing_selected_template()
    {
        var context = Context(true);
        var buffer = new SupportDiagnosticBuffer();
        var middleware = new SupportDiagnosticMiddleware(ctx =>
        {
            ctx.SetEndpoint(null); // Simulates exception handling re-execution.
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, buffer);

        var entry = Assert.Single(buffer.Snapshot());
        Assert.Equal(Route, entry.Route);
        Assert.Equal(500, entry.StatusCode);
        Assert.Equal("GET", entry.Method);
        Assert.Equal("synthetic-correlation-681", entry.CorrelationId);
        Assert.DoesNotContain("11111111-1111", entry.Route);
    }

    [Fact]
    public async Task Thrown_exception_still_records_route_without_exception_details()
    {
        var context = Context(true);
        var buffer = new SupportDiagnosticBuffer();
        var middleware = new SupportDiagnosticMiddleware(
            _ => throw new InvalidOperationException("Synthetic private exception detail"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context, buffer));

        var entry = Assert.Single(buffer.Snapshot());
        Assert.Equal(Route, entry.Route);
        Assert.Equal("error", entry.Severity);
        Assert.DoesNotContain("Synthetic", entry.Route);
    }

    [Fact]
    public async Task Unknown_request_never_persists_raw_path()
    {
        var context = Context(false);
        var buffer = new SupportDiagnosticBuffer();
        var middleware = new SupportDiagnosticMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context, buffer);

        var entry = Assert.Single(buffer.Snapshot());
        Assert.Equal("(sin ruta)", entry.Route);
        Assert.DoesNotContain("11111111", entry.Route);
    }
}
