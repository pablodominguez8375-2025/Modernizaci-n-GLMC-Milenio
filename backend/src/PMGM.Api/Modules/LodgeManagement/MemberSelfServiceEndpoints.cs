using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.LodgeManagement;

public static class MemberSelfServiceEndpoints
{
    public static IEndpointRouteBuilder MapMemberSelfServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial/mi-ficha")
            .WithTags("Gestión Logial — Mi Ficha")
            .RequireAuthorization();

        group.MapGet("", GetMyProfileAsync);
        group.MapPut("/contacto", UpdateMyContactAsync);

        return endpoints;
    }

    private static async Task<IResult> GetMyProfileAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        var actor = await memberContextResolver.ResolveAsync(httpContext.User, cancellationToken);
        if (actor is null) return Results.NotFound(new { message = "No existe una ficha institucional vinculada a la sesión." });

        var member = await db.Members
            .AsNoTracking()
            .Where(x => x.Id == actor.MemberId)
            .Select(x => new
            {
                x.Id,
                x.InstitutionalNumber,
                x.Person.FirstNames,
                x.Person.LastNames,
                x.Person.Email,
                x.Person.Phone,
                x.Person.Address
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (member is null) return Results.NotFound();

        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == actor.MemberId)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new MemberMembershipHistoryDto(
                x.Id,
                x.OrganizationId,
                x.Organization.Name,
                x.Organization.Number,
                x.StartDate,
                x.EndDate,
                x.Status))
            .ToListAsync(cancellationToken);

        var degreeEvents = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => x.MemberId == actor.MemberId)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new MemberDegreeHistoryDto(
                x.Id,
                x.OrganizationId,
                x.Organization.Name,
                x.Degree,
                x.EventType,
                x.EffectiveDate))
            .ToListAsync(cancellationToken);

        var offices = await db.OfficeAssignments
            .AsNoTracking()
            .Where(x => x.MemberId == actor.MemberId)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new MemberOfficeHistoryDto(
                x.Id,
                x.OrganizationId,
                x.Organization.Name,
                x.OfficeType,
                x.Period,
                x.StartDate,
                x.EndDate))
            .ToListAsync(cancellationToken);

        var statuses = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => x.MemberId == actor.MemberId)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new MemberStatusHistoryDto(
                x.Id,
                x.OrganizationId,
                x.Organization != null ? x.Organization.Name : null,
                x.EventType,
                x.EffectiveDate))
            .ToListAsync(cancellationToken);

        var currentMembership = memberships.FirstOrDefault(x => x.EndDate is null && x.Status == "active");

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new MemberSelfProfileDto(
            member.Id,
            member.FirstNames,
            member.LastNames,
            member.InstitutionalNumber,
            member.Email,
            member.Phone,
            member.Address,
            actor.EffectiveDegree,
            currentMembership,
            memberships,
            degreeEvents,
            offices,
            statuses,
            new[] { "email", "phone", "address" }));
    }

    private static async Task<IResult> UpdateMyContactAsync(
        UpdateMemberContactRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        var actor = await memberContextResolver.ResolveAsync(httpContext.User, cancellationToken);
        if (actor is null) return Results.NotFound(new { message = "No existe una ficha institucional vinculada a la sesión." });

        var email = NormalizeOptional(request.Email, 320);
        var phone = NormalizeOptional(request.Phone, 80);
        var address = NormalizeOptional(request.Address, 500);

        if (request.Email is not null && email is null && request.Email.Trim().Length > 0)
            return Results.BadRequest(new { message = "El correo excede el máximo permitido." });
        if (request.Phone is not null && phone is null && request.Phone.Trim().Length > 0)
            return Results.BadRequest(new { message = "El teléfono excede el máximo permitido." });
        if (request.Address is not null && address is null && request.Address.Trim().Length > 0)
            return Results.BadRequest(new { message = "El domicilio excede el máximo permitido." });
        if (email is not null && !IsValidEmail(email))
            return Results.BadRequest(new { message = "El correo electrónico no tiene un formato válido." });

        var member = await db.Members
            .Include(x => x.Person)
            .SingleOrDefaultAsync(x => x.Id == actor.MemberId, cancellationToken);
        if (member is null) return Results.NotFound();

        var changedFields = new List<string>(3);
        if (!string.Equals(member.Person.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            member.Person.Email = email;
            changedFields.Add("email");
        }
        if (!string.Equals(member.Person.Phone, phone, StringComparison.Ordinal))
        {
            member.Person.Phone = phone;
            changedFields.Add("phone");
        }
        if (!string.Equals(member.Person.Address, address, StringComparison.Ordinal))
        {
            member.Person.Address = address;
            changedFields.Add("address");
        }

        if (changedFields.Count > 0)
        {
            db.AuditEvents.Add(AuditEventFactory.Create(
                httpContext,
                "member.self_service.contact_updated",
                nameof(Person),
                member.PersonId.ToString(),
                organizationId: null,
                AuditResults.Success,
                new
                {
                    ChangedFields = changedFields,
                    ChangedCount = changedFields.Count
                }));
            await db.SaveChangesAsync(cancellationToken);
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new MemberContactDto(
            member.Id,
            member.Person.Email,
            member.Person.Phone,
            member.Person.Address,
            changedFields));
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (value is null) return null;
        var normalized = value.Trim();
        if (normalized.Length == 0) return null;
        return normalized.Length <= maxLength ? normalized : null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email);
            return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record UpdateMemberContactRequest(string? Email, string? Phone, string? Address);

public sealed record MemberContactDto(
    Guid MemberId,
    string? Email,
    string? Phone,
    string? Address,
    IReadOnlyCollection<string> ChangedFields);

public sealed record MemberMembershipHistoryDto(
    Guid MembershipId,
    Guid OrganizationId,
    string OrganizationName,
    string? OrganizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status);

public sealed record MemberDegreeHistoryDto(
    Guid DegreeEventId,
    Guid OrganizationId,
    string OrganizationName,
    string Degree,
    string EventType,
    DateOnly EffectiveDate);

public sealed record MemberOfficeHistoryDto(
    Guid OfficeAssignmentId,
    Guid OrganizationId,
    string OrganizationName,
    string OfficeType,
    string Period,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record MemberStatusHistoryDto(
    Guid StatusEventId,
    Guid? OrganizationId,
    string? OrganizationName,
    string EventType,
    DateOnly EffectiveDate);

public sealed record MemberSelfProfileDto(
    Guid MemberId,
    string FirstNames,
    string LastNames,
    string? InstitutionalNumber,
    string? Email,
    string? Phone,
    string? Address,
    int EffectiveDegree,
    MemberMembershipHistoryDto? CurrentMembership,
    IReadOnlyCollection<MemberMembershipHistoryDto> MembershipHistory,
    IReadOnlyCollection<MemberDegreeHistoryDto> DegreeHistory,
    IReadOnlyCollection<MemberOfficeHistoryDto> OfficeHistory,
    IReadOnlyCollection<MemberStatusHistoryDto> StatusHistory,
    IReadOnlyCollection<string> EditableFields);
