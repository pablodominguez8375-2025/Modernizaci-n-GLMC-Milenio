using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement;

namespace PMGM.Api.Modules.Membership;

// No selector de miembro: el vínculo institucional del token es la única fuente de identidad.
public static class MemberOwnHistoryEndpoints
{
    public static IEndpointRouteBuilder MapMemberOwnHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/membership/me").WithTags("Mi ficha").RequireAuthorization();
        group.MapGet("/cargos", OfficesAsync);
        group.MapGet("/asistencias", AttendanceAsync);
        group.MapGet("/hospitalaria", HospitalariaAsync);
        return endpoints;
    }

    private static async Task<IResult> OfficesAsync(HttpContext http, PmgmDbContext db,
        IInstitutionalMemberContextResolver resolver, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var own = await resolver.ResolveAsync(http.User, ct);
        if (own is null) return Results.NotFound();
        var items = await db.OfficeAssignments.AsNoTracking().Where(x => x.MemberId == own.MemberId)
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, cargo = x.OfficeType, periodo = x.Period, tallerId = x.OrganizationId,
                taller = x.Organization.Name, desde = x.StartDate, hasta = x.EndDate }).ToListAsync(ct);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> AttendanceAsync(string? tipo, DateOnly? desde, DateOnly? hasta,
        HttpContext http, PmgmDbContext db, LodgeManagementDbContext lodgeDb,
        IInstitutionalMemberContextResolver resolver, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var own = await resolver.ResolveAsync(http.User, ct);
        if (own is null) return Results.NotFound();
        if (tipo is not null && tipo is not ("tenida" or "ceremonia" or "instruccion"))
            return Results.BadRequest(new { message = "Tipo válido: tenida, ceremonia o instruccion." });
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var from = desde ?? today.AddMonths(-12); var through = hasta ?? today;
        if (from > through) return Results.BadRequest(new { message = "El rango de fechas no es válido." });
        var meetings = await lodgeDb.LodgeAttendanceRecords.AsNoTracking()
            .Where(x => x.MemberId == own.MemberId && x.Meeting.MeetingDate >= from && x.Meeting.MeetingDate <= through &&
                (x.Meeting.Status == LodgeManagementCodes.MeetingStatus.Held || x.Meeting.Status == LodgeManagementCodes.MeetingStatus.Closed))
            .Select(x => new { id = x.MeetingId, fecha = x.Meeting.MeetingDate, ceremonia = x.Meeting.CeremonyType,
                tema = x.Meeting.Title, tallerId = x.Meeting.OrganizationId, estado = x.Status, x.RecordedAtUtc }).ToListAsync(ct);
        var instructions = await lodgeDb.LodgeInstructionAttendanceRecords.AsNoTracking()
            .Where(x => x.MemberId == own.MemberId && x.InstructionSession.InstructionDate >= from && x.InstructionSession.InstructionDate <= through &&
                x.InstructionSession.Status == LodgeManagementCodes.InstructionStatus.Held)
            .Select(x => new { id = x.InstructionSessionId, fecha = x.InstructionSession.InstructionDate,
                tema = x.InstructionSession.Topic, tallerId = x.InstructionSession.OrganizationId, estado = x.Status, x.RecordedAtUtc }).ToListAsync(ct);
        var rows = meetings.GroupBy(x => x.id).Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
            .Select(x => new OwnAttendanceItem(x.id, x.fecha, string.IsNullOrEmpty(x.ceremonia) ? "tenida" : "ceremonia", x.tema, x.tallerId, "", Translate(x.estado)))
            .Concat(instructions.GroupBy(x => x.id).Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
                .Select(x => new OwnAttendanceItem(x.id, x.fecha, "instruccion", x.tema, x.tallerId, "", Translate(x.estado))))
            .Where(x => tipo == null || x.Tipo == tipo).OrderByDescending(x => x.Fecha).ThenBy(x => x.Id).ToList();
        var ids = rows.Select(x => x.TallerId).Distinct().ToList();
        var names = await db.Organizations.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var items = rows.Select(x => x with { Taller = names.GetValueOrDefault(x.TallerId, "Taller") }).ToList();
        return Results.Ok(new { desde = from, hasta = through, items,
            resumen = new { total = items.Count, presente = items.Count(x => x.Estado == "presente"),
                justificado = items.Count(x => x.Estado == "justificado"), ausente = items.Count(x => x.Estado == "ausente") } });
    }

    private static async Task<IResult> HospitalariaAsync(HttpContext http, PmgmDbContext db,
        IInstitutionalMemberContextResolver resolver, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var own = await resolver.ResolveAsync(http.User, ct);
        if (own is null) return Results.NotFound();
        var rows = await db.DeathReplenishmentObligations.AsNoTracking().Include(x => x.Payments)
            .Include(x => x.Case).ThenInclude(x => x.DeceasedMember).ThenInclude(x => x.Person)
            .Include(x => x.Case).ThenInclude(x => x.Rate).Include(x => x.Organization)
            .Where(x => x.MemberId == own.MemberId).OrderByDescending(x => x.Case.DeathDate).ToListAsync(ct);
        var items = rows.Select(x => new { x.Id, caseId = x.CaseId, tallerId = x.OrganizationId, taller = x.Organization.Name,
            fecha = x.Case.DeathDate, hermanoFallecido = $"{x.Case.DeceasedMember.Person.FirstNames} {x.Case.DeceasedMember.Person.LastNames}",
            moneda = "CLP", monto = x.AmountDue, pagado = x.Payments.Sum(p => p.Amount), saldo = x.AmountDue - x.Payments.Sum(p => p.Amount), estado = x.Status,
            fechaPago = x.Status == "paid" ? x.Payments.Max(p => (DateOnly?)p.PaymentDate) : null,
            comprobantes = x.Payments.Select(p => new { p.ReceiptNumber, p.PaymentDate, p.Amount, p.Reference }),
            decreto = x.Case.Rate == null ? null : new { numero = x.Case.Rate.DecreeNumber, fecha = x.Case.Rate.DecreeDate,
                vigencia = x.Case.Rate.EffectiveFrom, respaldo = x.Case.Rate.SourceReference } }).ToList();
        return Results.Ok(new { total = items.Count, items });
    }

    private static string Translate(string value) => value switch { "present" => "presente", "excused" => "justificado", _ => "ausente" };
}
public sealed record OwnAttendanceItem(Guid Id, DateOnly Fecha, string Tipo, string? Tema, Guid TallerId, string Taller, string Estado);
