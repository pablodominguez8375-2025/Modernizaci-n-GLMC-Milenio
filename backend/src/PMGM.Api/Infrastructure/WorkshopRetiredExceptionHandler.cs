using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace PMGM.Api.Infrastructure;

/// <summary>#1039: SQLSTATE PM409 (guardia de Taller dado de baja) => 409 WORKSHOP_RETIRED, sin datos del registro.</summary>
public sealed class WorkshopRetiredExceptionHandler : IExceptionHandler
{
    public const string SqlState = "PM409";
    public const string Code = "WORKSHOP_RETIRED";

    public static bool IsWorkshopRetired(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: SqlState, MessageText: Code }) return true;
        return false;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (!IsWorkshopRetired(exception)) return false;
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { code = Code, message = "El Taller está dado de baja: solo lectura." }, ct);
        return true;
    }
}
