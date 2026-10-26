using Microsoft.AspNetCore.Routing;

namespace PMGM.Api.Modules.Audit;

/// <summary>
/// Bounded, volatile, metadata-only HTTP diagnostics.
/// No payloads, headers, principal names, remote IPs, exception text or URL parameters.
/// This is NOT a replacement for protected host/centralized logs.
/// </summary>
public sealed record SupportDiagnosticEntry(
    DateTimeOffset OccurredAtUtc,
    string Severity,
    int StatusCode,
    string Method,
    string Route,
    string CorrelationId);

public sealed class SupportDiagnosticBuffer
{
    public const int Capacity = 500;
    private readonly object _gate = new();
    private readonly Queue<SupportDiagnosticEntry> _entries = new();

    public void Record(SupportDiagnosticEntry entry)
    {
        if (entry.StatusCode < 400 || entry.StatusCode > 599)
            return;

        // Metadata must be from server-controlled sources, not user-controlled URL.
        var normalized = new SupportDiagnosticEntry(
            entry.OccurredAtUtc,
            entry.StatusCode >= 500 ? "error" : "warning",
            entry.StatusCode,
            NormalizeMethod(entry.Method),
            NormalizeRoute(entry.Route),
            NormalizeCorrelationId(entry.CorrelationId));
        lock (_gate)
        {
            if (_entries.Count == Capacity) _entries.Dequeue();
            _entries.Enqueue(normalized);
        }
    }

    public SupportDiagnosticEntry[] Snapshot()
    {
        lock (_gate) return _entries.Reverse().ToArray();
    }

    private static string NormalizeMethod(string method)
        => method.ToUpperInvariant() is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS"
            ? method.ToUpperInvariant() : "OTHER";

    private static string NormalizeRoute(string route)
    {
        // Only framework-defined route templates may be submitted. Unknown
        // requests always use a generic fixed value in the middleware.
        return string.IsNullOrWhiteSpace(route) ? "(sin ruta)" : route.Length <= 180
            ? route : "(ruta omitida)";
    }

    private static string NormalizeCorrelationId(string correlationId)
    {
        if (correlationId.Length > 100 || correlationId.Any(c =>
                !(char.IsLetterOrDigit(c) || c is '-' or '_' or ':' or '.')))
            return "(no disponible)";
        return correlationId;
    }
}

public sealed class SupportDiagnosticMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SupportDiagnosticBuffer buffer)
    {
        var recorded = false;
        // Router-selected *template* is safe metadata. Snapshot BEFORE the
        // inner exception handler: it can clear HttpContext.GetEndpoint() while
        // handling an exception, otherwise a known API route becomes "(sin ruta)".
        // Never retain Request.Path, route values, query, claims or the exception.
        var selectedTemplate = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        try
        {
            // Inner exception handler turns exceptions into status 500.
            await next(context);
        }
        catch (Exception)
        {
            if (context.RequestAborted.IsCancellationRequested) throw;
            Capture(StatusCodes.Status500InternalServerError);
            recorded = true;
            throw;
        }
        finally
        {
            if (!recorded && !context.RequestAborted.IsCancellationRequested &&
                context.Response.StatusCode >= 400)
                Capture(context.Response.StatusCode);
        }

        void Capture(int status)
        {
            // Do not use Request.Path, QueryString, endpoint display names,
            // user-provided headers or exception messages.
            var template = selectedTemplate
                           ?? (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                           ?? "(sin ruta)";
            buffer.Record(new SupportDiagnosticEntry(
                DateTimeOffset.UtcNow, "", status, context.Request.Method,
                template, context.TraceIdentifier));
        }
    }
}
