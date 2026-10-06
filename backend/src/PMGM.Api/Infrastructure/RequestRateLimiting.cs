using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace PMGM.Api.Infrastructure;

public static class RequestRateLimiting
{
    public static IServiceCollection AddInstitutionalRequestRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var writes = ReadPositive(configuration, "WritesPerMinute", 120);
        var uploads = ReadPositive(configuration, "UploadsPerMinute", 20);
        var exports = ReadPositive(configuration, "ExportsPerMinute", 30);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var category = Category(context.Request);
                if (category is null)
                    return RateLimitPartition.GetNoLimiter("unrestricted-read");

                // Authentication has already run. Never partition on caller-supplied headers,
                // path, query, role or Taller: changing those must not reset the user's budget.
                var subject = context.User.FindFirst("sub")
                    ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
                var actor = context.User.Identity?.IsAuthenticated == true && subject is not null
                    ? $"user:{subject.Issuer}:{subject.Value}"
                    : $"peer:{context.Connection.RemoteIpAddress}";
                var limit = category == "upload" ? uploads : category == "export" ? exports : writes;
                return RateLimitPartition.GetFixedWindowLimiter($"{category}:{actor}", _ => new()
                {
                    PermitLimit = limit,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true,
                    QueueLimit = 0
                });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                        .ToString(CultureInfo.InvariantCulture);
                response.Headers.CacheControl = "no-store";
                await response.WriteAsJsonAsync(new
                {
                    message = "Has realizado demasiadas solicitudes. Espera un momento y vuelve a intentar."
                }, cancellationToken);
            };
        });
        return services;
    }

    private static int ReadPositive(IConfiguration configuration, string key, int fallback)
    {
        var value = configuration.GetValue<int?>($"RequestRateLimiting:{key}") ?? fallback;
        if (value <= 0) throw new InvalidOperationException($"RequestRateLimiting:{key} debe ser positivo.");
        return value;
    }

    private static string? Category(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)) return null;
        // Routing accepts a trailing slash; it must not bypass the content/ICS budget.
        var path = (request.Path.Value ?? "").TrimEnd('/');
        var content = path.EndsWith("/contenido", StringComparison.OrdinalIgnoreCase);
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            return content || path.EndsWith("/ics", StringComparison.OrdinalIgnoreCase)
                ? "export" : null;
        if (HttpMethods.IsOptions(request.Method)) return null;
        if (content && (HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method)))
            return "upload";
        return "write";
    }
}
