using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.RegimenInterior;

public static class DataQualityCaseEndpoints
{
    public static IEndpointRouteBuilder MapDataQualityCaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/regimen-interior/data-quality/cases")
            .WithTags("Régimen Interior — Corroboración")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/{caseId:guid}", GetAsync);
        group.MapPost("/", OpenAsync);
        group.MapPost("/{caseId:guid}/claim", ClaimAsync);
        group.MapPost("/{caseId:guid}/resolve", ResolveAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string? status,
        string? ruleCode,
        Guid? organizationId,
        bool? assignedToMe,
        int? limit,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IDataQualityCaseService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();
        var normalizedStatus = Normalize(status);
        if (normalizedStatus is not null && !DataQualityCaseCodes.Status.IsValid(normalizedStatus))
            return Results.BadRequest(new { message = "El estado de caso indicado no es válido." });

        var response = await service.ListAsync(new DataQualityCaseListQuery(
            normalizedStatus,
            Normalize(ruleCode),
            organizationId,
            assignedToMe ?? false,
            Math.Clamp(limit ?? 200, 1, 500)),
            Subject(httpContext.User),
            cancellationToken);
        NoStore(httpContext);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetAsync(
        Guid caseId,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IDataQualityCaseService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();
        var item = await service.GetAsync(caseId, cancellationToken);
        if (item is null) return Results.NotFound();
        NoStore(httpContext);
        return Results.Ok(item);
    }

    private static async Task<IResult> OpenAsync(
        OpenDataQualityCaseRequest request,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IDataQualityCaseService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();
        var actor = Actor(httpContext.User);
        if (actor is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.RuleCode) || request.RuleCode.Length > 160)
            return Results.BadRequest(new { message = "El código de regla es obligatorio y debe ser válido." });
        var severity = Normalize(request.Severity);
        if (severity is not DataQualitySeverity.Error and not DataQualitySeverity.Warning)
            return Results.BadRequest(new { message = "La severidad debe ser error o warning." });

        try
        {
            var result = await service.OpenAsync(new OpenDataQualityCaseCommand(
                request.DetectionAsOf,
                request.RuleCode.Trim().ToLowerInvariant(),
                severity,
                request.MemberId,
                request.OrganizationId,
                request.PrimaryDate,
                request.RelatedDate), actor, cancellationToken);
            NoStore(httpContext);
            return result.Created
                ? Results.Created($"/api/regimen-interior/data-quality/cases/{result.Case.Id}", result.Case)
                : Results.Ok(result.Case);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (DataQualityFindingNotCurrentException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ClaimAsync(
        Guid caseId,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IDataQualityCaseService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();
        var actor = Actor(httpContext.User);
        if (actor is null) return Results.Unauthorized();

        try
        {
            var result = await service.ClaimAsync(caseId, actor, cancellationToken);
            NoStore(httpContext);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (InvalidCaseTransitionException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (CaseAlreadyAssignedException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ResolveAsync(
        Guid caseId,
        ResolveDataQualityCaseRequest request,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IDataQualityCaseService service,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();
        var actor = Actor(httpContext.User);
        if (actor is null) return Results.Unauthorized();
        var outcome = Normalize(request.Outcome);
        if (outcome is not DataQualityResolutionOutcome.Confirmed and not DataQualityResolutionOutcome.Dismissed)
            return Results.BadRequest(new { message = "El resultado debe ser confirmed o dismissed." });
        if (string.IsNullOrWhiteSpace(request.ResolutionSummary) || request.ResolutionSummary.Trim().Length < 10 || request.ResolutionSummary.Length > 2000)
            return Results.BadRequest(new { message = "La resolución debe describir brevemente el resultado de la corroboración (10 a 2000 caracteres)." });
        if (request.EvidenceReference?.Length > 500)
            return Results.BadRequest(new { message = "La referencia de respaldo supera el máximo permitido." });

        try
        {
            var result = await service.ResolveAsync(
                caseId,
                new ResolveDataQualityCaseCommand(outcome, request.ResolutionSummary, request.EvidenceReference),
                actor,
                access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin),
                cancellationToken);
            NoStore(httpContext);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (InvalidCaseTransitionException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (CaseAssignedToAnotherReviewerException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static CaseActor? Actor(ClaimsPrincipal user)
    {
        var subject = Subject(user);
        if (string.IsNullOrWhiteSpace(subject)) return null;
        var displayName = user.Identity?.Name ?? user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name);
        return new CaseActor(subject, displayName);
    }

    private static string? Subject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static void NoStore(HttpContext context)
        => context.Response.Headers.CacheControl = "private, no-store";
}

public sealed record OpenDataQualityCaseRequest(
    DateOnly DetectionAsOf,
    string RuleCode,
    string Severity,
    Guid MemberId,
    Guid? OrganizationId,
    DateOnly? PrimaryDate,
    DateOnly? RelatedDate);

public sealed record ResolveDataQualityCaseRequest(
    string Outcome,
    string ResolutionSummary,
    string? EvidenceReference);
