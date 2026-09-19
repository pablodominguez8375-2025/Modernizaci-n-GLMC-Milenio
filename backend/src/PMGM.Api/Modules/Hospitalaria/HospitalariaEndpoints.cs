using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Hospitalaria.Entities;

namespace PMGM.Api.Modules.Hospitalaria;

public static class HospitalariaEndpoints
{
    public static IEndpointRouteBuilder MapHospitalariaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/hospitalaria")
            .WithTags("Gran Hospitalaria")
            .RequireAuthorization();

        group.MapPost("/talleres/{organizationId:guid}/regularidad", SetWorkshopRegularityAsync);
        group.MapGet("/talleres/{organizationId:guid}/regularidad", GetWorkshopRegularityAsync);
        group.MapGet("/rendiciones", ListMonthlySubmissionsAsync);
        group.MapPost("/rendiciones/{submissionId:guid}/revision", ReviewMonthlySubmissionAsync);

        return endpoints;
    }

    private static async Task<IResult> SetWorkshopRegularityAsync(
        Guid organizationId,
        HospitalariaRegularityRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageHospitalariaRegularity(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!HospitalariaCodes.RegularityStatus.IsValid(request.Status))
        {
            return Results.BadRequest(new { message = "El estado de regularidad hospitalaria indicado no es válido." });
        }

        var organizationExists = await db.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return Results.NotFound(new { message = "El Taller u organización no existe." });
        }

        var snapshot = new HospitalariaRegularitySnapshot
        {
            OrganizationId = organizationId,
            Status = request.Status,
            AsOfDate = request.AsOfDate,
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.HospitalariaRegularitySnapshots.Add(snapshot);
        audit.Add(
            httpContext,
            "hospitalaria.workshop_regularity.recorded",
            nameof(HospitalariaRegularitySnapshot),
            snapshot.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { snapshot.Status, snapshot.AsOfDate });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/hospitalaria/talleres/{organizationId}/regularidad",
            ToAdministrativeResponse(snapshot));
    }

    private static async Task<IResult> GetWorkshopRegularityAsync(
        Guid organizationId,
        DateOnly? asOf,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadInstitutionalRegularity(httpContext.User) &&
            !access.CanReadOrganization(httpContext.User, organizationId))
        {
            return Results.Forbid();
        }

        var cutoff = asOf ?? TodayInChile();
        var snapshot = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AsOfDate <= cutoff)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return Results.NotFound(new { message = "No existe una validación de Gran Hospitalaria para la fecha indicada." });
        }

        if (access.CanManageHospitalariaRegularity(httpContext.User))
        {
            return Results.Ok(ToAdministrativeResponse(snapshot));
        }

        return Results.Ok(ToMinimalProjection(snapshot));
    }

    private static async Task<IResult> ListMonthlySubmissionsAsync(
        Guid? organizationId,
        int? year,
        int? month,
        string? status,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageHospitalariaRegularity(httpContext.User)) return Results.Forbid();

        if (month is < 1 or > 12)
            return Results.BadRequest(new { message = "El mes indicado no es válido." });
        if (!string.IsNullOrWhiteSpace(status) && !HospitalariaCodes.SubmissionStatus.IsValid(status))
            return Results.BadRequest(new { message = "El estado de rendición no es válido." });

        var query = db.HospitalariaMonthlySubmissions
            .AsNoTracking()
            .Include(x => x.Organization)
            .Where(x => x.Status != HospitalariaCodes.SubmissionStatus.Draft);

        if (organizationId is not null) query = query.Where(x => x.OrganizationId == organizationId.Value);
        if (year is not null) query = query.Where(x => x.PeriodYear == year.Value);
        if (month is not null) query = query.Where(x => x.PeriodMonth == month.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var items = await query
            .OrderByDescending(x => x.PeriodYear)
            .ThenByDescending(x => x.PeriodMonth)
            .ThenByDescending(x => x.SubmittedAtUtc)
            .Take(250)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                organizationName = x.Organization.Name,
                organizationNumber = x.Organization.Number,
                x.PeriodYear,
                x.PeriodMonth,
                x.CutoffDate,
                x.IncomeAmount,
                x.ApprovedExpenseAmount,
                x.PeriodNetAmount,
                x.MovementCount,
                x.PendingExpenseCount,
                x.ReplenishmentDueAmount,
                x.ReplenishmentPaidAmount,
                x.DifferenceAmount,
                x.PaymentReference,
                x.CouncilFinancialReviewId,
                x.Status,
                x.SourceReference,
                x.SubmittedAtUtc,
                x.ReviewedAtUtc,
                x.ReviewNotes
            })
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> ReviewMonthlySubmissionAsync(
        Guid submissionId,
        HospitalariaSubmissionReviewRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageHospitalariaRegularity(httpContext.User)) return Results.Forbid();
        if (!HospitalariaCodes.ReviewDecision.IsValid(request.Decision))
            return Results.BadRequest(new { message = "La decisión indicada no es válida." });
        if (request.Decision == HospitalariaCodes.ReviewDecision.Observed && string.IsNullOrWhiteSpace(request.Notes))
            return Results.BadRequest(new { message = "Una observación de Gran Hospitalaria debe indicar el motivo." });

        var submission = await db.HospitalariaMonthlySubmissions
            .SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
        if (submission is null) return Results.NotFound();
        if (submission.Status != HospitalariaCodes.SubmissionStatus.Submitted)
            return Results.Conflict(new { message = "Sólo una rendición enviada puede ser revisada por Gran Hospitalaria." });

        var reviewer = GetSubject(httpContext.User);
        var now = DateTimeOffset.UtcNow;

        if (request.Decision == HospitalariaCodes.ReviewDecision.Reconciled)
        {
            if (submission.DifferenceAmount > 0)
                return Results.Conflict(new { message = "No puede conciliarse una rendición con reposiciones pendientes." });

            submission.Status = HospitalariaCodes.SubmissionStatus.Reconciled;
            submission.ReviewedBySubject = reviewer;
            submission.ReviewedAtUtc = now;
            submission.ReviewNotes = Normalize(request.Notes);

            db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot
            {
                OrganizationId = submission.OrganizationId,
                Status = HospitalariaCodes.RegularityStatus.UpToDate,
                AsOfDate = submission.CutoffDate,
                SourceReference = $"hospitalaria-rendicion:{submission.Id}",
                Notes = "Regularidad derivada de rendición mensual conciliada por Gran Hospitalaria."
            });

            audit.Add(
                httpContext,
                "hospitalaria.monthly_submission.reconciled",
                nameof(HospitalariaMonthlySubmission),
                submission.Id.ToString(),
                submission.OrganizationId,
                AuditResults.Success,
                new
                {
                    submission.PeriodYear,
                    submission.PeriodMonth,
                    submission.ReplenishmentDueAmount,
                    submission.ReplenishmentPaidAmount,
                    submission.DifferenceAmount
                });
        }
        else
        {
            submission.Status = HospitalariaCodes.SubmissionStatus.Observed;
            submission.ReviewedBySubject = reviewer;
            submission.ReviewedAtUtc = now;
            submission.ReviewNotes = request.Notes!.Trim();

            db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot
            {
                OrganizationId = submission.OrganizationId,
                Status = submission.DifferenceAmount > 0
                    ? HospitalariaCodes.RegularityStatus.Overdue
                    : HospitalariaCodes.RegularityStatus.Pending,
                AsOfDate = submission.CutoffDate,
                SourceReference = $"hospitalaria-rendicion:{submission.Id}",
                Notes = "Regularidad observada por Gran Hospitalaria; revisar la rendición agregada."
            });

            audit.Add(
                httpContext,
                "hospitalaria.monthly_submission.observed",
                nameof(HospitalariaMonthlySubmission),
                submission.Id.ToString(),
                submission.OrganizationId,
                AuditResults.Observed,
                new
                {
                    submission.PeriodYear,
                    submission.PeriodMonth,
                    submission.DifferenceAmount
                });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            submission.Id,
            submission.OrganizationId,
            submission.Status,
            submission.ReviewedAtUtc,
            submission.ReviewNotes
        });
    }

    private static HospitalariaRegularityProjectionDto ToMinimalProjection(HospitalariaRegularitySnapshot snapshot)
        => new(snapshot.Status, snapshot.AsOfDate);

    private static object ToAdministrativeResponse(HospitalariaRegularitySnapshot snapshot) => new
    {
        snapshot.Id,
        snapshot.OrganizationId,
        snapshot.Status,
        snapshot.AsOfDate,
        snapshot.SourceReference,
        snapshot.Notes,
        snapshot.RecordedAtUtc
    };

    private static DateOnly TodayInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(chileNow.DateTime);
    }

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record HospitalariaRegularityProjectionDto(
    string Status,
    DateOnly AsOfDate);

public sealed record HospitalariaRegularityRequest(
    string Status,
    DateOnly AsOfDate,
    string? SourceReference,
    string? Notes);

public sealed record HospitalariaSubmissionReviewRequest(
    string Decision,
    string? Notes);
