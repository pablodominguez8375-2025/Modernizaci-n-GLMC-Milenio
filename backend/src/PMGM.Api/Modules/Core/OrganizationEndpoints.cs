using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.Treasury;
using System.Security.Cryptography;

namespace PMGM.Api.Modules.Core;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/institutional/organizations/options", GetOrganizationOptionsAsync)
            .WithTags("Organizaciones institucionales")
            .RequireAuthorization();

        endpoints.MapGet("/api/institutional/organizations/{id:guid}/profile", GetOrganizationProfileAsync)
            .WithTags("Organizaciones institucionales")
            .RequireAuthorization();

        endpoints.MapPut("/api/institutional/organizations/{id:guid}/profile/metadata", UpdateOrganizationMetadataAsync)
            .WithTags("Organizaciones institucionales")
            .RequireAuthorization();

        endpoints.MapGet("/api/institutional/organizations/{id:guid}/profile/logo", GetWorkshopLogoAsync).WithTags("Organizaciones institucionales").RequireAuthorization();
        endpoints.MapPut("/api/institutional/organizations/{id:guid}/profile/logo", PutWorkshopLogoAsync).WithTags("Organizaciones institucionales").RequireAuthorization();
        endpoints.MapDelete("/api/institutional/organizations/{id:guid}/profile/logo", DeleteWorkshopLogoAsync).WithTags("Organizaciones institucionales").RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetOrganizationOptionsAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        var rows = await db.Organizations
            .AsNoTracking()
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Number)
            .ThenBy(x => x.Name)
            .Select(x => new OrganizationOptionDto(x.Id, x.Name, x.Number, x.Type))
            .Take(2000)
            .ToListAsync(cancellationToken);

        var grantedOrganizationIds = new HashSet<Guid>();
        var memberContext = await memberContextResolver.ResolveAsync(httpContext.User, cancellationToken);
        if (memberContext is { EffectiveDegree: 3 })
        {
            var grantRows = await (from grant in db.LodgeSummaryAccessGrants.AsNoTracking()
                                   join membership in db.Memberships.AsNoTracking() on grant.MemberId equals membership.MemberId
                                   where grant.MemberId == memberContext.MemberId && grant.RevokedAtUtc == null &&
                                         membership.OrganizationId == grant.OrganizationId &&
                                         membership.Status == MembershipCodes.MembershipStatus.Active && membership.EndDate == null
                                   select grant.OrganizationId).Distinct().ToListAsync(cancellationToken);
            grantedOrganizationIds.UnionWith(grantRows);
        }

        var items = rows
            .Where(x => access.CanReadOrganization(httpContext.User, x.Id) || grantedOrganizationIds.Contains(x.Id))
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new OrganizationOptionsResponse(items.Count, items));
    }

    private static async Task<IResult> GetOrganizationProfileAsync(
        Guid id,
        HttpContext httpContext,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Number,
                x.Type,
                x.ParentOrganizationId,
                x.CreatedAtUtc,
                x.EstablishedOn,
                x.City,
                x.Country,
                x.OrienteCode,
                x.TreasuryTerritory,
                hasLogo = x.LogoObjectKey != null
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return Results.NotFound();
        }

        var canRead = (access.HasOrderScope(httpContext.User) && access.CanReadOrganization(httpContext.User, id)) ||
                      await LodgeSummaryAccessPolicy.CanReadAsync(httpContext.User, id, db, access, memberContextResolver, cancellationToken);
        if (!canRead)
        {
            return Results.Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeMemberIds = await db.Memberships
            .AsNoTracking()
            .Where(x => x.OrganizationId == id &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        x.EndDate == null)
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // El grado pertenece al hermano y debe viajar con él en un traslado.
        // Por eso se obtiene el último evento de grado global del miembro,
        // aunque haya ocurrido en su Taller de origen.
        var degreeEvents = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => activeMemberIds.Contains(x.MemberId))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.MemberId, x.Degree, x.EffectiveDate, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);

        var currentDegrees = degreeEvents
            .GroupBy(x => x.MemberId)
            .Select(group => group.First())
            .GroupBy(x => x.Degree)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var authorities = await db.OfficeAssignments
            .AsNoTracking()
            .Where(x => x.OrganizationId == id &&
                        x.StartDate <= today &&
                        (x.EndDate == null || x.EndDate >= today))
            .OrderBy(x => x.OfficeType)
            .ThenBy(x => x.Member.Person.LastNames)
            .ThenBy(x => x.Member.Person.FirstNames)
            .Select(x => new
            {
                x.Id,
                x.OfficeType,
                x.Period,
                x.MemberId,
                DisplayName = x.Member.Person.FirstNames + " " + x.Member.Person.LastNames,
                x.StartDate,
                x.EndDate
            })
            .ToListAsync(cancellationToken);

        var latestFinancial = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == id && x.MemberId == null)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.Status, x.AsOfDate, x.SourceReference })
            .FirstOrDefaultAsync(cancellationToken);

        var latestHospitalaria = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == id)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.Status, x.AsOfDate, x.SourceReference })
            .FirstOrDefaultAsync(cancellationToken);

        var meetings = await lodgeDb.LodgeMeetings
            .AsNoTracking()
            .Where(x => x.OrganizationId == id)
            .OrderByDescending(x => x.MeetingDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(8)
            .Select(x => new
            {
                x.Id,
                x.MeetingDate,
                x.MeetingType,
                x.Grade,
                x.Title,
                x.Status,
                x.ClosedAtUtc
            })
            .ToListAsync(cancellationToken);

        var instruction = await lodgeDb.LodgeInstructionSessions
            .AsNoTracking()
            .Where(x => x.OrganizationId == id)
            .OrderByDescending(x => x.InstructionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.InstructionDate,
                x.Grade,
                x.Topic,
                x.ResponsibleOffice,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var recentTransfers = await db.MemberTransfers
            .AsNoTracking()
            .Where(x => x.SourceOrganizationId == id || x.TargetOrganizationId == id)
            .OrderByDescending(x => x.RequestedDate)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                x.MemberId,
                MemberDisplayName = x.Member.Person.FirstNames + " " + x.Member.Person.LastNames,
                x.SourceOrganizationId,
                SourceOrganization = x.SourceOrganization.Name,
                x.TargetOrganizationId,
                TargetOrganization = x.TargetOrganization.Name,
                x.RequestedDate,
                x.ApprovedEffectiveDate,
                x.Status,
                Direction = x.SourceOrganizationId == id ? "outgoing" : "incoming"
            })
            .ToListAsync(cancellationToken);

        var canReadRegularity = OrganizationProfilePrivacy.CanReadRegularity(access, httpContext.User, id);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            organization,
            members = new
            {
                active = activeMemberIds.Count,
                degreeDistribution = currentDegrees
            },
            authorities,
            regularity = canReadRegularity ? new { financial = latestFinancial, hospitalaria = latestHospitalaria } : null,
            activity = new
            {
                recentMeetings = meetings,
                recentInstruction = instruction,
                recentTransfers
            }
        });
    }

    private static async Task<IResult> UpdateOrganizationMetadataAsync(
        Guid id,
        UpdateOrganizationMetadataRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (organization is null) return Results.NotFound();
        if (!access.CanManageWorkshopProfile(httpContext.User, id)) return Results.Forbid();
        if (!string.Equals(organization.Type, "workshop", StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();

        var todayInChile = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Santiago"));
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 ||
            (request.EstablishedOn.HasValue && request.EstablishedOn.Value > todayInChile) ||
            request.City?.Length > 120 || request.Country?.Length > 120)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["metadata"] = ["Revise la fecha de fundación (no puede ser futura) y los campos de ubicación (máximo 120 caracteres)."]
            });

        if (request.OrienteCode is not null && (!WorkshopOriente.IsValid(request.OrienteCode) ||
            (request.OrienteCode != "santiago" && string.IsNullOrWhiteSpace(request.City))))
            return Results.BadRequest(new { message = "Seleccione un Oriente válido e indique la ciudad para Regiones o Perú." });
        var previous = new { organization.Name, organization.EstablishedOn, organization.City, organization.Country, organization.OrienteCode, organization.TreasuryTerritory };
        organization.Name = name;
        organization.EstablishedOn = request.EstablishedOn;
        organization.City = NormalizeOptional(request.City);
        organization.Country = NormalizeOptional(request.Country);
        organization.OrienteCode = request.OrienteCode ?? WorkshopOriente.FromLocation(organization.City, organization.Country);
        if (request.OrienteCode is not null)
        {
            organization.Country = request.OrienteCode == "peru" ? "Perú" : "Chile";
            if (request.OrienteCode == "santiago") organization.City = "Santiago";
            if (WorkshopOriente.FromLocation(organization.City, organization.Country) != request.OrienteCode)
                return Results.BadRequest(new { message = "La ciudad no coincide con el Oriente seleccionado." });
        }
        organization.TreasuryTerritory = WorkshopOriente.Territory(organization);

        audit.Add(httpContext, "organization.workshop_profile.metadata_updated", nameof(Organization), id.ToString(), id,
            AuditResults.Success, new
            {
                previous,
                current = new { organization.Name, organization.EstablishedOn, organization.City, organization.Country, organization.OrienteCode, organization.TreasuryTerritory }
            });
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetWorkshopLogoAsync(Guid id, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IInstitutionalMemberContextResolver memberContextResolver, IDocumentObjectStore store, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (org is null || !string.Equals(org.Type, "workshop", StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
        var canRead = (access.HasOrderScope(context.User) && access.CanReadOrganization(context.User, id)) || await LodgeSummaryAccessPolicy.CanReadAsync(context.User, id, db, access, memberContextResolver, ct);
        if (!canRead) return Results.Forbid();
        if (org.LogoObjectKey is null || org.LogoContentType is null) return Results.NotFound();
        if (!await store.ExistsAsync(org.LogoObjectKey, ct)) return Results.NotFound();
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Stream(await store.OpenReadAsync(org.LogoObjectKey, ct), org.LogoContentType, enableRangeProcessing: false);
    }

    private static async Task<IResult> PutWorkshopLogoAsync(Guid id, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, IDocumentObjectStore store, IDocumentMalwareScanner scanner, CancellationToken ct)
    {
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (org is null || !string.Equals(org.Type, "workshop", StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
        if (!access.CanManageWorkshopProfile(context.User, id)) return Results.Forbid();
        if (context.Request.ContentLength is null or <= 0 or > WorkshopLogoContentTypePolicy.MaxUploadBytes) return Results.BadRequest(new { message = "El logo debe pesar entre 1 byte y 2 MiB." });
        var content = new byte[(int)context.Request.ContentLength.Value];
        var offset = 0;
        while (offset < content.Length)
        {
            var read = await context.Request.Body.ReadAsync(content.AsMemory(offset), ct);
            if (read == 0) return Results.BadRequest(new { message = "El archivo está incompleto." });
            offset += read;
        }
        var contentType = context.Request.ContentType?.Split(';')[0].Trim();
        if (!WorkshopLogoContentTypePolicy.IsAllowed(contentType, content)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["logo"] = ["Use un archivo PNG o JPEG válido."] });
        var key = $"organization-profile/{id:N}/logo/{Guid.NewGuid():N}";
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        DocumentMalwareScanResult scan;
        try { using var scanStream = new MemoryStream(content, writable: false); scan = await scanner.ScanAsync(scanStream, ct); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Net.Sockets.SocketException) { return Results.Problem("No se pudo verificar el archivo. Intente más tarde.", statusCode: 503); }
        if (!scan.IsClean) return Results.ValidationProblem(new Dictionary<string, string[]> { ["logo"] = ["El archivo fue rechazado por el análisis de seguridad."] });
        var oldKey = org.LogoObjectKey;
        await store.StoreAsync(key, new MemoryStream(content, writable: false), contentType!, ct);
        org.LogoObjectKey = key; org.LogoContentType = contentType; org.LogoSha256 = sha;
        audit.Add(context, "organization.workshop_profile.logo_updated", nameof(Organization), id.ToString(), id, AuditResults.Success, new { oldKey, newKey = key, sha256 = sha, scan.EvidenceReference });
        try { await db.SaveChangesAsync(ct); }
        catch { await store.DeleteAsync(key, ct); throw; }
        if (oldKey is not null) try { await store.DeleteAsync(oldKey, ct); } catch (IOException) { }
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteWorkshopLogoAsync(Guid id, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, IDocumentObjectStore store, CancellationToken ct)
    {
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (org is null || !string.Equals(org.Type, "workshop", StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
        if (!access.CanManageWorkshopProfile(context.User, id)) return Results.Forbid();
        var oldKey = org.LogoObjectKey;
        org.LogoObjectKey = null; org.LogoContentType = null; org.LogoSha256 = null;
        audit.Add(context, "organization.workshop_profile.logo_removed", nameof(Organization), id.ToString(), id, AuditResults.Success, new { oldKey });
        await db.SaveChangesAsync(ct);
        if (oldKey is not null) try { await store.DeleteAsync(oldKey, ct); } catch (IOException) { }
        return Results.NoContent();
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}

public sealed record UpdateOrganizationMetadataRequest(string? Name, DateOnly? EstablishedOn, string? City, string? Country, string? OrienteCode = null);

public sealed record OrganizationOptionDto(
    Guid Id,
    string Name,
    string? Number,
    string Type);

public sealed record OrganizationOptionsResponse(
    int Total,
    IReadOnlyList<OrganizationOptionDto> Items);
