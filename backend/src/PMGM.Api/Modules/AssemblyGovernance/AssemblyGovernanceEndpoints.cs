using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.AssemblyGovernance.Entities;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.AssemblyGovernance;

public static class AssemblyGovernanceEndpoints
{
    public static IEndpointRouteBuilder MapAssemblyGovernanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/asambleas")
            .WithTags("Asamblea General")
            .RequireAuthorization();

        group.MapPost("/", CreateAssemblyAsync);
        group.MapPost("/{assemblyId:guid}/habilitaciones/{personId:guid}/evaluar", EvaluateAsync);
        group.MapGet("/{assemblyId:guid}/padron", GetRosterAsync);
        group.MapPost("/{assemblyId:guid}/cerrar-padron", FreezeRosterAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAssemblyAsync(
        CreateAssemblyRequest request,
        HttpContext httpContext,
        AssemblyGovernanceDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "El nombre es obligatorio." });

        var assembly = new InstitutionalAssembly
        {
            Name = request.Name.Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "general" : request.Type.Trim(),
            AssemblyDate = request.AssemblyDate,
            Status = AssemblyGovernanceCodes.AssemblyStatus.RosterOpen,
            RosterCutoffUtc = request.RosterCutoffUtc
        };

        db.Assemblies.Add(assembly);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/asambleas/{assembly.Id}/padron", assembly);
    }

    private static async Task<IResult> EvaluateAsync(
        Guid assemblyId,
        Guid personId,
        Guid lodgeId,
        HttpContext httpContext,
        IAssemblyEligibilityService service,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();

        try
        {
            var result = await service.EvaluateAsync(
                assemblyId,
                personId,
                lodgeId,
                ActorSubject(httpContext.User),
                cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> GetRosterAsync(
        Guid assemblyId,
        HttpContext httpContext,
        AssemblyGovernanceDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();

        var assembly = await db.Assemblies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assemblyId, cancellationToken);
        if (assembly is null) return Results.NotFound();

        var roster = await db.AssemblyEligibilities
            .AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId)
            .OrderBy(x => x.LodgeId)
            .ThenBy(x => x.PersonId)
            .Select(x => new
            {
                x.PersonId,
                x.LodgeId,
                x.CanAttend,
                x.CanVote,
                x.Status,
                x.PrimaryReason,
                x.EvaluatedAtUtc,
                x.IsFrozenSnapshot,
                x.FrozenAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new { assembly, roster });
    }

    private static async Task<IResult> FreezeRosterAsync(
        Guid assemblyId,
        HttpContext httpContext,
        AssemblyGovernanceDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User)) return Results.Forbid();

        var assembly = await db.Assemblies.SingleOrDefaultAsync(x => x.Id == assemblyId, cancellationToken);
        if (assembly is null) return Results.NotFound();
        if (assembly.Status == AssemblyGovernanceCodes.AssemblyStatus.Frozen)
            return Results.Ok(new { assembly.Id, assembly.Status, assembly.FrozenAtUtc, alreadyFrozen = true });

        var now = DateTimeOffset.UtcNow;
        var rows = await db.AssemblyEligibilities.Where(x => x.AssemblyId == assemblyId).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.IsFrozenSnapshot = true;
            row.FrozenAtUtc = now;
        }

        assembly.Status = AssemblyGovernanceCodes.AssemblyStatus.Frozen;
        assembly.FrozenAtUtc = now;
        assembly.FrozenBySubject = ActorSubject(httpContext.User);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { assembly.Id, assembly.Status, assembly.FrozenAtUtc, frozenRows = rows.Count });
    }

    private static string ActorSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.Identity?.Name ?? "unknown";
}

public sealed record CreateAssemblyRequest(
    string Name,
    string? Type,
    DateOnly AssemblyDate,
    DateTimeOffset? RosterCutoffUtc);
