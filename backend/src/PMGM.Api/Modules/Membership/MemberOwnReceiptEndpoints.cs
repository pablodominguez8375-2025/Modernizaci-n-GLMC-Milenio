using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Membership;

// Comprobantes personales: la identidad proviene exclusivamente del token.
// Nunca aceptar un memberId ni exponer referencia bancaria o usuario registrador.
public static class MemberOwnReceiptEndpoints
{
    public static IEndpointRouteBuilder MapMemberOwnReceiptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/membership/me/comprobantes")
            .WithTags("Comprobantes de Mi ficha").RequireAuthorization();
        group.MapGet("/tesoreria/{id:guid}", TreasuryAsync);
        group.MapGet("/hospitalaria/{id:guid}", HospitalariaAsync);
        group.MapGet("/ceremonia", ListCeremonyAsync);
        group.MapGet("/ceremonia/{id:guid}", CeremonyAsync);
        return endpoints;
    }

    private static async Task<IResult> TreasuryAsync(Guid id, HttpContext http, PmgmDbContext db,
        IInstitutionalMemberContextResolver resolver, IAuditService audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var own = await resolver.ResolveAsync(http.User, ct);
        if (own is null) return Results.NotFound();

        // Recibo multiperíodo: se consulta el monto original, NO la suma de cuotas visibles en el frontend.
        var receipt = await db.LodgeMemberReceipts.AsNoTracking()
            .Include(x => x.Allocations).ThenInclude(x => x.Charge)
            .Include(x => x.Adjustments)
            .SingleOrDefaultAsync(x => x.Id == id && x.MemberId == own.MemberId, ct);
        if (receipt is not null)
        {
            var organization = await db.Organizations.AsNoTracking()
                .Where(x => x.Id == receipt.OrganizationId).Select(x => x.Name).FirstOrDefaultAsync(ct) ?? "Taller";
            var lines = receipt.Allocations.OrderBy(x => x.Charge.PeriodYear)
                .ThenBy(x => x.Charge.PeriodMonth).ThenBy(x => x.AllocatedAtUtc)
                .Select(x => new OwnReceiptLine("Imputación de cuota", x.Amount, x.Charge.PeriodYear, x.Charge.PeriodMonth))
                .ToList();
            var corrections = receipt.Adjustments.OrderBy(x => x.EffectiveDate)
                .Select(x => new OwnReceiptCorrection(x.Kind, x.EffectiveDate, x.CashAmount)).ToList();
            var balance = receipt.Amount + corrections.Sum(x => x.CashAmount) - lines.Sum(x => x.Amount);
            var view = new OwnReceiptView("tesoreria", receipt.ReceiptNumber, organization, receipt.Currency,
                receipt.PaymentDate, receipt.Amount, receipt.PaymentMethod, balance, lines, corrections);
            audit.Add(http, "member.self.receipt.downloaded", "LodgeMemberReceipt", receipt.Id.ToString(),
                receipt.OrganizationId, AuditResults.Success, new { origin = "treasury" });
            await db.SaveChangesAsync(ct);
            return Results.Ok(view);
        }

        // Pagos legados anteriores al registro multiperíodo: un comprobante por movimiento real.
        var legacy = await db.LodgeMemberPayments.AsNoTracking()
            .Include(x => x.Charge).ThenInclude(x => x.Organization)
            .SingleOrDefaultAsync(x => x.Id == id && x.Charge.MemberId == own.MemberId, ct);
        if (legacy is null) return Results.NotFound();
        var oldView = new OwnReceiptView("tesoreria", legacy.ReceiptNumber, legacy.Charge.Organization.Name,
            legacy.Currency, legacy.PaymentDate, legacy.Amount, legacy.PaymentMethod, 0m,
            new List<OwnReceiptLine> {
                new("Cuota registrada", legacy.Amount, legacy.Charge.PeriodYear, legacy.Charge.PeriodMonth)
            }, new List<OwnReceiptCorrection>());
        audit.Add(http, "member.self.receipt.downloaded", "LodgeMemberPayment", legacy.Id.ToString(),
            legacy.Charge.OrganizationId, AuditResults.Success, new { origin = "treasury-legacy" });
        await db.SaveChangesAsync(ct);
        return Results.Ok(oldView);
    }

    // Derechos de ceremonia: recibos ya vinculados a un hermano institucional.
    // Los candidatos todavía no iniciados no tienen MemberId: no se hacen
    // coincidencias débiles con nombre/RUT/persona candidata ni se acepta un
    // identificador de miembro remitido desde el navegador.
    private static async Task<IResult> ListCeremonyAsync(HttpContext http, PmgmDbContext db,
        IInstitutionalMemberContextResolver resolver, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var own = await resolver.ResolveAsync(http.User, ct);
        if (own is null) return Results.NotFound();

        var rows = await db.CeremonyRightPayments.AsNoTracking()
            .Where(x => x.CeremonyRequest.MemberId == own.MemberId)
            .OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new
            {
                x.Id, x.CeremonyRequestId, x.ReceiptNumber,
                x.PaymentDate, x.Amount, x.Currency, x.PaymentMethod,
                x.CeremonyRequest.CeremonyType,
                Organization = x.CeremonyRequest.Organization.Name
            })
            .ToListAsync(ct);

        var items = rows.Select(x => new OwnCeremonyReceiptSummary(
            x.Id, x.CeremonyRequestId, x.ReceiptNumber,
            x.Organization, x.CeremonyType, x.Currency,
            x.PaymentDate, x.Amount, x.PaymentMethod)).ToList();
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> CeremonyAsync(Guid id, HttpContext http, PmgmDbContext db,
        IInstitutionalMemberContextResolver resolver, IAuditService audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var own = await resolver.ResolveAsync(http.User, ct);
        if (own is null) return Results.NotFound();

        var payment = await db.CeremonyRightPayments.AsNoTracking()
            .Where(x => x.Id == id && x.CeremonyRequest.MemberId == own.MemberId)
            .Select(x => new
            {
                x.Id, x.ReceiptNumber, x.Currency, x.PaymentDate,
                x.Amount, x.PaymentMethod,
                x.CeremonyRequest.CeremonyType,
                x.CeremonyRequest.OrganizationId,
                Organization = x.CeremonyRequest.Organization.Name
            })
            .SingleOrDefaultAsync(ct);
        if (payment is null) return Results.NotFound();

        var view = new OwnReceiptView("ceremonia", payment.ReceiptNumber,
            payment.Organization, payment.Currency, payment.PaymentDate,
            payment.Amount, payment.PaymentMethod, 0m,
            new List<OwnReceiptLine>
            {
                new($"Derecho de ceremonia: {CeremonyLabel(payment.CeremonyType)}", payment.Amount)
            },
            new List<OwnReceiptCorrection>());
        audit.Add(http, "member.self.receipt.downloaded", "CeremonyRightPayment",
            payment.Id.ToString(), payment.OrganizationId, AuditResults.Success,
            new { origin = "ceremony" });
        await db.SaveChangesAsync(ct);
        return Results.Ok(view);
    }

    private static string CeremonyLabel(string type) => type switch
    {
        CeremonyCodes.Type.Initiation => "Iniciación",
        CeremonyCodes.Type.Affiliation => "Afiliación",
        CeremonyCodes.Type.WageIncrease => "Aumento de salario",
        CeremonyCodes.Type.Exaltation => "Exaltación",
        CeremonyCodes.Type.Incorporation => "Incorporación",
        _ => "Ceremonia"
    };

    private static async Task<IResult> HospitalariaAsync(Guid id, HttpContext http, PmgmDbContext db,
        IInstitutionalMemberContextResolver resolver, IAuditService audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var own = await resolver.ResolveAsync(http.User, ct);
        if (own is null) return Results.NotFound();
        var payment = await db.DeathReplenishmentPayments.AsNoTracking()
            .Include(x => x.Obligation).ThenInclude(x => x.Organization)
            .Include(x => x.Obligation).ThenInclude(x => x.Case)
                .ThenInclude(x => x.DeceasedMember).ThenInclude(x => x.Person)
            .SingleOrDefaultAsync(x => x.Id == id && x.Obligation.MemberId == own.MemberId, ct);
        if (payment is null) return Results.NotFound();

        var deceased = payment.Obligation.Case.DeceasedMember.Person;
        var view = new OwnReceiptView("hospitalaria", payment.ReceiptNumber, payment.Obligation.Organization.Name,
            "CLP", payment.PaymentDate, payment.Amount, payment.PaymentMethod, 0m,
            new List<OwnReceiptLine> {
                new($"Reposición por fallecimiento: {deceased.FirstNames} {deceased.LastNames}", payment.Amount)
            }, new List<OwnReceiptCorrection>());
        audit.Add(http, "member.self.receipt.downloaded", "DeathReplenishmentPayment", payment.Id.ToString(),
            payment.Obligation.OrganizationId, AuditResults.Success, new { origin = "hospitalaria" });
        await db.SaveChangesAsync(ct);
        return Results.Ok(view);
    }
}

public sealed record OwnReceiptLine(string Description, decimal Amount, int? PeriodYear = null, int? PeriodMonth = null);
public sealed record OwnReceiptCorrection(string Kind, DateOnly Date, decimal CashAmount);
public sealed record OwnReceiptView(string Kind, string ReceiptNumber, string Organization, string Currency,
    DateOnly PaymentDate, decimal ReceivedAmount, string PaymentMethod, decimal UnallocatedAmount,
    IReadOnlyList<OwnReceiptLine> Lines, IReadOnlyList<OwnReceiptCorrection> Corrections);

public sealed record OwnCeremonyReceiptSummary(Guid Id, Guid CeremonyRequestId,
    string ReceiptNumber, string Organization, string CeremonyType, string Currency,
    DateOnly PaymentDate, decimal Amount, string PaymentMethod);
