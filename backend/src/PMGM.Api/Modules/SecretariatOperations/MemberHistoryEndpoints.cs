using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.SecretariatOperations.Entities;

namespace PMGM.Api.Modules.SecretariatOperations;

/// <summary>
/// Historia del hermano: la Secretaría del Taller propone (o corrige) cargos y fechas de iniciación, aumento de
/// salario y exaltación; Régimen Interior aprueba, observa o rechaza. Solo la aprobación modifica el historial
/// oficial. Una corrección reemplaza el registro vigente con motivo y auditoría (valor anterior y nuevo).
/// </summary>
public static class MemberHistoryEndpoints
{
    public static class Status
    {
        public const string Submitted = "submitted";
        public const string Approved = "approved";
        public const string Observed = "observed";
        public const string Rejected = "rejected";
    }

    private static readonly string[] DegreeEventTypes =
        [MembershipCodes.DegreeEvent.Initiation, MembershipCodes.DegreeEvent.WageIncrease, MembershipCodes.DegreeEvent.Exaltation];

    private static readonly HashSet<string> OfficeCodes =
        BootstrapCodes.WorkshopOffices.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);

    private const int MaxItems = 40;

    public static IEndpointRouteBuilder MapMemberHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var lodge = endpoints.MapGroup("/api/secretaria/talleres/{organizationId:guid}/hermanos/{memberId:guid}/historia")
            .WithTags("Secretaría")
            .RequireAuthorization();
        lodge.MapGet("", GetForLodgeAsync);
        lodge.MapPost("", ProposeAsync);

        var ri = endpoints.MapGroup("/api/regimen-interior/historia-hermanos")
            .WithTags("Régimen Interior")
            .RequireAuthorization();
        ri.MapGet("", ListPendingAsync);
        ri.MapPost("/{proposalId:guid}/resolver", ResolveAsync);
        return endpoints;
    }

    // ---------------------------------------------------------------- Secretaría del Taller

    private static async Task<IResult> GetForLodgeAsync(
        Guid organizationId, Guid memberId, HttpContext http, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeSecretariat(http.User, organizationId)) return Results.Forbid();
        if (!await IsLodgeMemberAsync(db, organizationId, memberId, ct)) return Results.NotFound();
        http.Response.Headers.CacheControl = "private, no-store";

        var offices = await db.OfficeAssignments.AsNoTracking()
            .Where(x => x.MemberId == memberId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new OfficeDto(x.Id, x.OfficeType, x.Period, x.StartDate, x.EndDate, x.EvidenceReference))
            .ToListAsync(ct);
        var degrees = await db.DegreeEvents.AsNoTracking()
            .Where(x => x.MemberId == memberId && DegreeEventTypes.Contains(x.EventType))
            .OrderBy(x => x.EffectiveDate)
            .Select(x => new DegreeDto(x.Id, x.EventType, x.EffectiveDate, x.EvidenceReference))
            .ToListAsync(ct);
        var proposals = await db.MemberHistoryProposals.AsNoTracking().Include(x => x.Items)
            .Where(x => x.MemberId == memberId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(20)
            .ToListAsync(ct);

        return Results.Ok(new
        {
            offices,
            degreeEvents = degrees,
            officeOptions = BootstrapCodes.WorkshopOffices.OrderBy(x => x.SortOrder).Select(x => new { code = x.Code, name = x.Name }),
            proposals = proposals.Select(ToProposalDto),
        });
    }

    private static async Task<IResult> ProposeAsync(
        Guid organizationId, Guid memberId, MemberHistoryProposalRequest request, HttpContext http,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeSecretariat(http.User, organizationId)) return Results.Forbid();
        if (!await IsLodgeMemberAsync(db, organizationId, memberId, ct))
            return Results.NotFound(new { message = "El hermano no pertenece a este Taller." });

        if (await db.MemberHistoryProposals.AnyAsync(x => x.MemberId == memberId && x.Status == Status.Submitted, ct))
            return Results.Conflict(new { message = "Este hermano ya tiene una propuesta pendiente en Régimen Interior. Espere su resolución." });

        var existingOffices = await db.OfficeAssignments.AsNoTracking()
            .Where(x => x.MemberId == memberId && x.OrganizationId == organizationId).ToListAsync(ct);
        var existingDegrees = await db.DegreeEvents.AsNoTracking()
            .Where(x => x.MemberId == memberId && DegreeEventTypes.Contains(x.EventType)).ToListAsync(ct);

        var error = Validate(request, existingOffices, existingDegrees);
        if (error is not null) return Results.BadRequest(new { message = error });

        var proposal = new MemberHistoryProposal
        {
            OrganizationId = organizationId,
            MemberId = memberId,
            Status = Status.Submitted,
            EvidenceReference = request.EvidenceReference.Trim(),
            Reason = request.Reason.Trim(),
            CreatedBySubject = Subject(http.User),
            Items = request.Items.Select(x => new MemberHistoryProposalItem
            {
                Kind = x.Kind,
                Action = x.Action,
                TargetRecordId = x.TargetRecordId,
                OfficeType = x.Kind == "office" ? x.OfficeType?.Trim() : null,
                Period = x.Kind == "office" ? x.Period?.Trim() : null,
                StartDate = x.Kind == "office" ? x.StartDate : null,
                EndDate = x.Kind == "office" ? x.EndDate : null,
                EventType = x.Kind == "degree" ? x.EventType : null,
                EventDate = x.Kind == "degree" ? x.EventDate : null,
            }).ToList(),
        };
        db.MemberHistoryProposals.Add(proposal);
        db.AuditEvents.Add(AuditEventFactory.Create(http, "secretariat.member_history.proposed", nameof(MemberHistoryProposal),
            proposal.Id.ToString(), organizationId, AuditResults.Success,
            new { itemCount = proposal.Items.Count, replacements = proposal.Items.Count(x => x.Action == "replace") }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/secretaria/talleres/{organizationId}/hermanos/{memberId}/historia", ToProposalDto(proposal));
    }

    // ---------------------------------------------------------------- Régimen Interior

    private static async Task<IResult> ListPendingAsync(
        Guid? organizationId, HttpContext http, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanReviewHistoricalMemberIntake(http.User)) return Results.Forbid();
        http.Response.Headers.CacheControl = "private, no-store";

        var query = db.MemberHistoryProposals.AsNoTracking().Include(x => x.Items)
            .Where(x => x.Status == Status.Submitted);
        if (organizationId is not null) query = query.Where(x => x.OrganizationId == organizationId);
        var proposals = await query.OrderBy(x => x.CreatedAtUtc).Take(500).ToListAsync(ct);

        var memberIds = proposals.Select(x => x.MemberId).Distinct().ToList();
        var orgIds = proposals.Select(x => x.OrganizationId).Distinct().ToList();
        var people = await db.Members.AsNoTracking().Where(x => memberIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Person.FirstNames, x.Person.LastNames, x.InstitutionalNumber }).ToListAsync(ct);
        var orgs = await db.Organizations.AsNoTracking().Where(x => orgIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var offices = await db.OfficeAssignments.AsNoTracking().Where(x => memberIds.Contains(x.MemberId)).ToListAsync(ct);
        var degrees = await db.DegreeEvents.AsNoTracking().Where(x => memberIds.Contains(x.MemberId) && DegreeEventTypes.Contains(x.EventType)).ToListAsync(ct);

        var items = proposals.Select(p =>
        {
            var person = people.SingleOrDefault(x => x.Id == p.MemberId);
            return new
            {
                p.Id,
                p.OrganizationId,
                organizationName = orgs.GetValueOrDefault(p.OrganizationId),
                p.MemberId,
                memberName = person is null ? null : $"{person.FirstNames} {person.LastNames}".Trim(),
                institutionalNumber = person?.InstitutionalNumber,
                p.EvidenceReference,
                p.Reason,
                p.CreatedAtUtc,
                items = p.Items.Select(i => new
                {
                    i.Id, i.Kind, i.Action, i.TargetRecordId, i.OfficeType, i.Period, i.StartDate, i.EndDate, i.EventType, i.EventDate,
                    before = i.Action != "replace" ? null : i.Kind == "office"
                        ? offices.Where(o => o.Id == i.TargetRecordId)
                            .Select(o => (object)new { o.OfficeType, o.Period, o.StartDate, o.EndDate }).FirstOrDefault()
                        : degrees.Where(d => d.Id == i.TargetRecordId)
                            .Select(d => (object)new { eventType = d.EventType, eventDate = d.EffectiveDate }).FirstOrDefault(),
                }),
            };
        });
        return Results.Ok(new { total = proposals.Count, items });
    }

    private static async Task<IResult> ResolveAsync(
        Guid proposalId, MemberHistoryResolveRequest request, HttpContext http, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanReviewHistoricalMemberIntake(http.User)) return Results.Forbid();
        if (request.Decision is not ("approve" or "observe" or "reject"))
            return Results.BadRequest(new { message = "La decisión debe ser approve, observe o reject." });
        if (request.Decision != "approve" && string.IsNullOrWhiteSpace(request.Notes))
            return Results.BadRequest(new { message = "La observación o el rechazo debe indicar el motivo." });

        var proposal = await db.MemberHistoryProposals.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == proposalId, ct);
        if (proposal is null) return Results.NotFound();
        if (proposal.Status != Status.Submitted)
            return Results.Conflict(new { message = "Solo una propuesta pendiente puede resolverse." });

        proposal.ReviewedBySubject = Subject(http.User);
        proposal.ReviewedAtUtc = DateTimeOffset.UtcNow;
        proposal.ReviewNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (request.Decision != "approve")
        {
            proposal.Status = request.Decision == "observe" ? Status.Observed : Status.Rejected;
            db.AuditEvents.Add(AuditEventFactory.Create(http, $"regimen_interior.member_history.{(request.Decision == "observe" ? "observed" : "rejected")}",
                nameof(MemberHistoryProposal), proposal.Id.ToString(), proposal.OrganizationId, AuditResults.Success, new { proposal.Id }));
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToProposalDto(proposal));
        }

        var offices = await db.OfficeAssignments
            .Where(x => x.MemberId == proposal.MemberId && x.OrganizationId == proposal.OrganizationId).ToListAsync(ct);
        var degrees = await db.DegreeEvents
            .Where(x => x.MemberId == proposal.MemberId && DegreeEventTypes.Contains(x.EventType)).ToListAsync(ct);

        var recheck = Validate(ToRequest(proposal), offices, degrees);
        if (recheck is not null)
            return Results.Conflict(new { message = $"El historial cambió desde la propuesta: {recheck} Pida a la Secretaría que la presente de nuevo." });

        var changes = new List<object>();
        foreach (var item in proposal.Items)
        {
            if (item.Kind == "office")
            {
                if (item.Action == "add")
                {
                    db.OfficeAssignments.Add(new OfficeAssignment
                    {
                        MemberId = proposal.MemberId, OrganizationId = proposal.OrganizationId,
                        OfficeType = item.OfficeType!, Period = item.Period!, StartDate = item.StartDate, EndDate = item.EndDate,
                        EvidenceReference = proposal.EvidenceReference,
                    });
                    changes.Add(new { kind = "office", action = "add", office = item.OfficeType, item.Period });
                }
                else
                {
                    var target = offices.Single(x => x.Id == item.TargetRecordId);
                    changes.Add(new
                    {
                        kind = "office", action = "replace", target.Id,
                        before = new { office = target.OfficeType, target.Period, target.StartDate, target.EndDate },
                        after = new { office = item.OfficeType, item.Period, item.StartDate, item.EndDate },
                    });
                    target.OfficeType = item.OfficeType!; target.Period = item.Period!;
                    target.StartDate = item.StartDate; target.EndDate = item.EndDate;
                    target.EvidenceReference = proposal.EvidenceReference;
                }
            }
            else if (item.Action == "add")
            {
                db.DegreeEvents.Add(new DegreeEvent
                {
                    MemberId = proposal.MemberId, OrganizationId = proposal.OrganizationId,
                    Degree = DegreeFor(item.EventType!), EventType = item.EventType!, EffectiveDate = item.EventDate!.Value,
                    EvidenceReference = proposal.EvidenceReference,
                });
                changes.Add(new { kind = "degree", action = "add", milestone = item.EventType, item.EventDate });
            }
            else
            {
                var target = degrees.Single(x => x.Id == item.TargetRecordId);
                changes.Add(new { kind = "degree", action = "replace", target.Id, milestone = item.EventType, before = target.EffectiveDate, after = item.EventDate });
                target.EffectiveDate = item.EventDate!.Value;
                target.EvidenceReference = proposal.EvidenceReference;
            }
        }

        proposal.Status = Status.Approved;
        db.AuditEvents.Add(AuditEventFactory.Create(http, "regimen_interior.member_history.approved", nameof(MemberHistoryProposal),
            proposal.Id.ToString(), proposal.OrganizationId, AuditResults.Success,
            new { proposal.Reason, proposal.EvidenceReference, changes }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToProposalDto(proposal));
    }

    // ---------------------------------------------------------------- Validación

    public static string? Validate(
        MemberHistoryProposalRequest request,
        IReadOnlyCollection<OfficeAssignment> offices,
        IReadOnlyCollection<DegreeEvent> degrees)
    {
        if (request.Items is null || request.Items.Count == 0) return "Agregue al menos un cambio.";
        if (request.Items.Count > MaxItems) return $"Una propuesta admite hasta {MaxItems} cambios.";
        if (string.IsNullOrWhiteSpace(request.EvidenceReference)) return "Indique la fuente o evidencia (acta, plancha, certificado).";
        if (request.EvidenceReference.Trim().Length > 500) return "La evidencia admite hasta 500 caracteres.";
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10) return "Indique el motivo (al menos 10 caracteres).";
        if (request.Reason.Trim().Length > 1000) return "El motivo admite hasta 1000 caracteres.";

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var minDate = new DateOnly(1900, 1, 1);
        var touched = new HashSet<Guid>();
        var newOffices = new HashSet<string>(StringComparer.Ordinal);
        var newEvents = new HashSet<string>(StringComparer.Ordinal);
        var resulting = DegreeEventTypes.ToDictionary(t => t, t => degrees.Where(d => d.EventType == t).Select(d => (DateOnly?)d.EffectiveDate).Min());

        foreach (var item in request.Items)
        {
            if (item.Action is not ("add" or "replace")) return "La acción de cada cambio debe ser agregar o corregir.";
            if (item.Action == "replace" && item.TargetRecordId is null) return "Un cambio de corrección debe indicar qué registro reemplaza.";
            if (item.Action == "replace" && !touched.Add(item.TargetRecordId!.Value)) return "No puede corregir dos veces el mismo registro en una propuesta.";

            if (item.Kind == "office")
            {
                if (string.IsNullOrWhiteSpace(item.OfficeType) || !OfficeCodes.Contains(item.OfficeType.Trim())) return "Elija un cargo de la lista.";
                if (string.IsNullOrWhiteSpace(item.Period) || item.Period.Trim().Length > 80) return "Indique el período del cargo (por ejemplo 2019-2020).";
                if (item.StartDate is { } s && (s < minDate || s > tomorrow)) return "La fecha de inicio del cargo no es válida.";
                if (item.EndDate is { } e && (e < minDate || e > tomorrow)) return "La fecha de término del cargo no es válida.";
                if (item.StartDate is not null && item.EndDate is not null && item.EndDate < item.StartDate) return "El término del cargo no puede ser anterior a su inicio.";
                var type = item.OfficeType.Trim(); var period = item.Period.Trim();
                if (item.Action == "replace")
                {
                    if (!offices.Any(o => o.Id == item.TargetRecordId)) return "El cargo que intenta corregir no existe en este Taller.";
                    if (offices.Any(o => o.Id != item.TargetRecordId && o.OfficeType == type && o.Period == period)) return $"Ya existe el cargo «{type}» en el período {period}.";
                }
                else if (offices.Any(o => o.OfficeType == type && o.Period == period)) return $"Ya existe el cargo «{type}» en el período {period}. Use «Corregir» si hay un error.";
                if (!newOffices.Add($"{type}|{period}")) return $"El cargo «{type}» del período {period} está repetido en la propuesta.";
            }
            else if (item.Kind == "degree")
            {
                if (item.EventType is null || !DegreeEventTypes.Contains(item.EventType)) return "Elija iniciación, aumento de salario o exaltación.";
                if (item.EventDate is not { } d || d < minDate || d > tomorrow) return "Indique una fecha válida (no futura) para el hito.";
                if (!newEvents.Add(item.EventType)) return "Cada hito (iniciación, aumento, exaltación) puede aparecer una sola vez por propuesta.";
                if (item.Action == "replace")
                {
                    var target = degrees.FirstOrDefault(x => x.Id == item.TargetRecordId);
                    if (target is null || target.EventType != item.EventType) return "El hito que intenta corregir no existe.";
                }
                else if (degrees.Any(x => x.EventType == item.EventType)) return "Ese hito ya tiene fecha. Use «Corregir» si hay un error.";
                resulting[item.EventType] = d;
            }
            else return "Cada cambio debe ser un cargo o un hito de grado.";
        }

        if (!SecretariatOperationsPolicy.HasChronologicalMilestones(
                resulting[MembershipCodes.DegreeEvent.Initiation], resulting[MembershipCodes.DegreeEvent.WageIncrease], resulting[MembershipCodes.DegreeEvent.Exaltation]))
            return "Las fechas de iniciación, aumento de salario y exaltación no son cronológicamente consistentes.";
        return null;
    }

    private static string DegreeFor(string eventType) => eventType switch
    {
        MembershipCodes.DegreeEvent.Initiation => SecretariatOperationsCodes.CurrentDegree.Apprentice,
        MembershipCodes.DegreeEvent.WageIncrease => SecretariatOperationsCodes.CurrentDegree.Fellowcraft,
        _ => SecretariatOperationsCodes.CurrentDegree.Master,
    };

    private static Task<bool> IsLodgeMemberAsync(PmgmDbContext db, Guid organizationId, Guid memberId, CancellationToken ct)
        => db.Memberships.AsNoTracking().AnyAsync(x => x.MemberId == memberId && x.OrganizationId == organizationId, ct);

    private static string Subject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static MemberHistoryProposalRequest ToRequest(MemberHistoryProposal p) => new(
        p.EvidenceReference, p.Reason,
        p.Items.Select(i => new MemberHistoryItemRequest(i.Kind, i.Action, i.TargetRecordId, i.OfficeType, i.Period, i.StartDate, i.EndDate, i.EventType, i.EventDate)).ToList());

    private static object ToProposalDto(MemberHistoryProposal p) => new
    {
        p.Id, p.OrganizationId, p.MemberId, p.Status, p.EvidenceReference, p.Reason, p.CreatedAtUtc, p.ReviewedAtUtc, p.ReviewNotes,
        items = p.Items.Select(i => new { i.Id, i.Kind, i.Action, i.TargetRecordId, i.OfficeType, i.Period, i.StartDate, i.EndDate, i.EventType, i.EventDate }),
    };
}

public sealed record OfficeDto(Guid Id, string OfficeType, string Period, DateOnly? StartDate, DateOnly? EndDate, string? EvidenceReference);
public sealed record DegreeDto(Guid Id, string EventType, DateOnly EffectiveDate, string? EvidenceReference);

public sealed record MemberHistoryItemRequest(
    string Kind, string Action, Guid? TargetRecordId,
    string? OfficeType, string? Period, DateOnly? StartDate, DateOnly? EndDate,
    string? EventType, DateOnly? EventDate);

public sealed record MemberHistoryProposalRequest(
    string EvidenceReference, string Reason, IReadOnlyList<MemberHistoryItemRequest> Items);

public sealed record MemberHistoryResolveRequest(string Decision, string? Notes);
