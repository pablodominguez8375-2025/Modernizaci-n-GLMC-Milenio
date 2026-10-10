using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Notifications;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// El Venerable formaliza la programación acordada por Consejo/Cámara.
/// No crea una votación ni presupone su aprobación: exige referencia al acta.
/// </summary>
public static class CandidateInterviewAssignmentEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/taller/expedientes-para-entrevista", CasesAsync);
        group.MapGet("/solicitudes/{requestId:guid}/maestros-entrevistadores", EligibleMastersAsync);
        group.MapGet("/solicitudes/{requestId:guid}/entrevistadores-designados", GetAssignmentsAsync);
        group.MapPost("/solicitudes/{requestId:guid}/entrevistadores-designados", AssignAsync);
        group.MapPost("/solicitudes/{requestId:guid}/entrevistadores-notificaciones", RetryNotificationsAsync);
        group.MapGet("/entrevistas/mis-designaciones", MineAsync);
        group.MapPost("/solicitudes/{requestId:guid}/entrevistadores-designados/{assignmentId:guid}/entregar", DeliverAsync);
    }

    private static DateOnly Today()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);

    private static bool CanDesignate(HttpContext http, IInstitutionalAccessService access, Guid orgId)
        => access.CanManageLodgeCouncilSummaryAccess(http.User, orgId) &&
           access.HasRole(http.User, InstitutionalRoles.TallerVenerable);

    private static bool CanObserve(HttpContext http, IInstitutionalAccessService access, Guid orgId)
        => CanDesignate(http, access, orgId) ||
           access.CanReadLodgeSecretariat(http.User, orgId);

    private static string? Subject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static async Task<IResult> CasesAsync(
        HttpContext http, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        var workshops = http.User.FindAll(InstitutionalClaims.Organization)
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty && CanDesignate(http, access, id)).Distinct().ToArray();
        if (workshops.Length == 0) return Results.Forbid();
        var rows = await db.CeremonyRequests.AsNoTracking()
            .Where(x => workshops.Contains(x.OrganizationId) &&
                x.CeremonyType == CeremonyCodes.Type.Initiation &&
                x.Status != CeremonyCodes.RequestStatus.Authorized &&
                x.Status != CeremonyCodes.RequestStatus.Rejected &&
                db.CandidatePublications.Any(p =>
                    p.CeremonyRequestId == x.Id &&
                    (p.Status == CeremonyCodes.PublicationStatus.Published ||
                     p.Status == CeremonyCodes.PublicationStatus.Completed)) &&
                db.CeremonyValidations.Any(v => v.CeremonyRequestId == x.Id &&
                    v.ValidationType == CeremonyCodes.ValidationType.CandidateInitialDeliberation &&
                    v.Status == CeremonyCodes.ValidationStatus.Approved))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id, x.OrganizationId,
                DisplayName = x.CandidatePerson == null ? "Sin nombre" :
                    x.CandidatePerson.FirstNames + " " + x.CandidatePerson.LastNames,
                Workshop = x.Organization.Name
            })
            .Take(100).ToListAsync(ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { items = rows });
    }

    private static async Task<IResult> EligibleMastersAsync(
        Guid requestId, HttpContext http, PmgmDbContext db, DocumentManagementDbContext documents,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId && x.CeremonyType == CeremonyCodes.Type.Initiation, ct);
        if (ceremony is null) return Results.NotFound();
        if (!CanDesignate(http, access, ceremony.OrganizationId)) return Results.Forbid();
        var eligible = await EligibleMasters(db, ceremony.OrganizationId, ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { items = eligible.Values.OrderBy(x => x.Name).Select(x => new { id = x.MemberId, name = x.Name }) });
    }

    private sealed record EligibleMaster(Guid MemberId, string Name);

    internal static async Task<bool> IsActiveDesignatedMasterAsync(
        PmgmDbContext db, Guid organizationId, Guid memberId, CancellationToken ct)
        => (await EligibleMasters(db, organizationId, ct)).ContainsKey(memberId);

    private static async Task<Dictionary<Guid, EligibleMaster>> EligibleMasters(
        PmgmDbContext db, Guid organizationId, CancellationToken ct)
    {
        var today = Today();
        var statuses = new[] { "active", "reinstated", "past_active", "inactive",
            "voluntary_withdrawal", "forced_withdrawal", "deceased" };
        var eligible = await db.Memberships.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId &&
                m.Status == MembershipCodes.MembershipStatus.Active &&
                (m.StartDate == null || m.StartDate <= today) &&
                (m.EndDate == null || m.EndDate >= today) &&
                (!db.InstitutionalStatusEvents.Any(e =>
                    e.MemberId == m.MemberId && e.EffectiveDate <= today &&
                    statuses.Contains(e.EventType)) ||
                 new[] { "active", "reinstated" }.Contains(
                    db.InstitutionalStatusEvents
                        .Where(e => e.MemberId == m.MemberId && e.EffectiveDate <= today &&
                            statuses.Contains(e.EventType))
                        .OrderByDescending(e => e.EffectiveDate)
                        .ThenByDescending(e => e.RecordedAtUtc)
                        .Select(e => e.EventType).FirstOrDefault()!)))
            .Select(m => new
            {
                m.MemberId, m.Member.CurrentDegree,
                Name = m.Member.Person.FirstNames + " " + m.Member.Person.LastNames
            }).Distinct().ToListAsync(ct);

        return eligible
            .Where(x => InstitutionalDegree.TryParse(x.CurrentDegree, out var degree) && degree >= 3)
            .GroupBy(x => x.MemberId)
            .ToDictionary(group => group.Key,
                group => new EligibleMaster(group.Key, group.First().Name));
    }

    private static async Task<IResult> GetAssignmentsAsync(
        Guid requestId, HttpContext http, PmgmDbContext db, DocumentManagementDbContext documents,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId && x.CeremonyType == CeremonyCodes.Type.Initiation, ct);
        if (ceremony is null) return Results.NotFound();
        if (!CanObserve(http, access, ceremony.OrganizationId)) return Results.Forbid();
        var records = await db.CandidateInterviewAssignments.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId)
            .OrderByDescending(x => x.AssignedAtUtc).ToListAsync(ct);
        var memberIds = records.Select(x => x.InterviewerMemberId).Distinct().ToArray();
        var names = await db.Members.AsNoTracking()
            .Where(m => memberIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Person.FirstNames, m.Person.LastNames })
            .ToListAsync(ct);
        var byId = names.ToDictionary(x => x.Id,
            x => (x.FirstNames + " " + x.LastNames).Trim());
        var reportIds = records.Where(x => x.ReportDocumentVersionId != null)
            .Select(x => x.ReportDocumentVersionId!.Value).Distinct().ToArray();
        var reports = await documents.DocumentVersions.AsNoTracking()
            .Where(x => reportIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Document.DocumentDate,
                x.Document.ShortDescription, x.Document.OfficialDocumentType })
            .ToListAsync(ct);
        var reportById = reports.ToDictionary(x => x.Id);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { items = records.Select(x => new
        {
            x.Id, x.Position, x.InterviewerMemberId,
            interviewerName = byId.GetValueOrDefault(x.InterviewerMemberId, "Miembro no disponible"),
            x.CouncilBody, x.CouncilDecisionDate, x.CouncilMinuteReference,
            x.ScheduledDate, x.Status, x.AssignedAtUtc,
            x.ReplacedAtUtc, x.ReplacementReason, x.CompletedAtUtc,
            hasReport = x.ReportDocumentVersionId != null,
            x.ReportDocumentVersionId,
            reportDate = reportById.ContainsKey(x.ReportDocumentVersionId.GetValueOrDefault())
                ? reportById[x.ReportDocumentVersionId!.Value].DocumentDate : null,
            reportResult = reportById.ContainsKey(x.ReportDocumentVersionId.GetValueOrDefault())
                ? reportById[x.ReportDocumentVersionId!.Value].OfficialDocumentType : null,
            reportSummary = reportById.ContainsKey(x.ReportDocumentVersionId.GetValueOrDefault())
                ? reportById[x.ReportDocumentVersionId!.Value].ShortDescription : null,
            notified = x.NotificationQueuedAtUtc != null
        }) });
    }

    private static async Task<IResult> AssignAsync(
        Guid requestId, AssignCandidateInterviewers input, HttpContext http,
        PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        IInstitutionalNotificationService notifications, CancellationToken ct)
    {
        var ceremony = await db.CeremonyRequests
            .SingleOrDefaultAsync(x => x.Id == requestId && x.CeremonyType == CeremonyCodes.Type.Initiation, ct);
        if (ceremony is null) return Results.NotFound();
        if (!CanDesignate(http, access, ceremony.OrganizationId)) return Results.Forbid();
        var actor = Subject(http.User);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Forbid();
        var error = CandidateInterviewAssignmentPolicy.Validate(
            input.InterviewerMemberIds, input.CouncilBody, input.CouncilDecisionDate,
            Today(), input.CouncilMinuteReference);
        if (error is not null) return Results.BadRequest(new { message = error });
        if (input.ScheduledDates is not null &&
            (input.ScheduledDates.Count != input.InterviewerMemberIds.Count ||
             input.ScheduledDates.Any(d => d.HasValue && d.Value < input.CouncilDecisionDate)))
            return Results.BadRequest(new { message = "Las fechas propuestas deben corresponder a cada Maestro y ser posteriores al acuerdo." });
        if (ceremony.Status is CeremonyCodes.RequestStatus.Rejected or CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "El expediente ya se encuentra cerrado." });
        var published = await db.CandidatePublications.AsNoTracking().AnyAsync(p =>
            p.CeremonyRequestId == requestId &&
            (p.Status == CeremonyCodes.PublicationStatus.Published ||
             p.Status == CeremonyCodes.PublicationStatus.Completed) &&
            p.PublishedFromUtc <= DateTimeOffset.UtcNow, ct);
        var deliberation = await db.CeremonyValidations.AsNoTracking()
            .Where(v => v.CeremonyRequestId == requestId &&
                v.ValidationType == CeremonyCodes.ValidationType.CandidateInitialDeliberation)
            .OrderByDescending(v => v.RecordedAtUtc).Select(v => v.Status).FirstOrDefaultAsync(ct);
        if (!published || deliberation != CeremonyCodes.ValidationStatus.Approved)
            return Results.Conflict(new { message = "Requiere deliberación unánime aprobada y publicación institucional vigente." });

        var eligible = await EligibleMasters(db, ceremony.OrganizationId, ct);
        if (input.InterviewerMemberIds.Any(id => !eligible.ContainsKey(id)))
            return Results.Conflict(new { message = "Se seleccionó un Hermano que no es Maestro activo de este Taller." });

        // No simular avisos para personas sin identidad institucional: deben crearse sus cuentas primero.
        var subjects = new Dictionary<Guid, string>();
        foreach (var member in input.InterviewerMemberIds)
        {
            var identities = await db.Database.SqlQuery<string>($"""
                SELECT "Subject" AS "Value" FROM core.member_identity_links
                WHERE "MemberId" = {member} AND "RevokedAtUtc" IS NULL
                """).ToListAsync(ct);
            if (identities.Count != 1)
                return Results.Conflict(new
                {
                    message = "Cada Maestro designado debe tener una cuenta institucional activa y vinculada de forma unívoca.",
                    memberId = member
                });
            subjects[member] = identities[0];
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({requestId.ToString()}, 0))", ct);
        var prior = await db.CandidateInterviewAssignments
            .Where(x => x.CeremonyRequestId == requestId &&
                (x.Status == CandidateInterviewAssignmentPolicy.Assigned ||
                 x.Status == CandidateInterviewAssignmentPolicy.Completed))
            .ToListAsync(ct);
        if (prior.Any(x => x.Status == CandidateInterviewAssignmentPolicy.Completed))
            return Results.Conflict(new { message = "Existe un informe entregado; para modificar su designación debe abrirse revisión formal." });
        if (prior.Count > 0 &&
            (string.IsNullOrWhiteSpace(input.ReplacementReason) ||
             input.ReplacementReason.Trim().Length is < 10 or > 1000))
            return Results.Conflict(new { message = "Para reemplazar entrevistadores se requiere motivo de 10 a 1000 caracteres." });

        var now = DateTimeOffset.UtcNow;
        foreach (var old in prior)
        {
            old.Status = CandidateInterviewAssignmentPolicy.Replaced;
            old.ReplacedAtUtc = now;
            old.ReplacedBySubject = actor;
            old.ReplacementReason = input.ReplacementReason!.Trim();
        }
        var added = input.InterviewerMemberIds.Select((member, i) => new CandidateInterviewAssignment
        {
            CeremonyRequestId = requestId,
            OrganizationId = ceremony.OrganizationId,
            InterviewerMemberId = member,
            Position = i + 1,
            CouncilBody = input.CouncilBody,
            CouncilDecisionDate = input.CouncilDecisionDate,
            CouncilMinuteReference = input.CouncilMinuteReference.Trim(),
            ScheduledDate = input.ScheduledDates is null ? null : input.ScheduledDates[i],
            Status = CandidateInterviewAssignmentPolicy.Assigned,
            AssignedBySubject = actor
        }).ToArray();
        db.CandidateInterviewAssignments.AddRange(added);
        audit.Add(http, "candidate.interview.designation.recorded", nameof(CandidateInterviewAssignment),
            requestId.ToString(), ceremony.OrganizationId, AuditResults.Success,
            new
            {
                council = input.CouncilBody,
                input.CouncilDecisionDate,
                minuteReference = input.CouncilMinuteReference.Trim(),
                assignedMemberIds = input.InterviewerMemberIds,
                assignmentIds = added.Select(x => x.Id).ToArray(),
                replacedAssignmentIds = prior.Select(x => x.Id).ToArray()
            });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var pending = await NotifyAsync(added, subjects, notifications, db, ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            requestId,
            assignmentIds = added.Select(x => x.Id),
            assigned = added.Length,
            notificationsPending = pending
        });
    }

    private static async Task<IResult> RetryNotificationsAsync(
        Guid requestId, HttpContext http, PmgmDbContext db,
        IInstitutionalAccessService access, IInstitutionalNotificationService service,
        CancellationToken ct)
    {
        var c = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId && x.CeremonyType == CeremonyCodes.Type.Initiation, ct);
        if (c is null) return Results.NotFound();
        if (!CanDesignate(http, access, c.OrganizationId)) return Results.Forbid();
        var rows = await db.CandidateInterviewAssignments
            .Where(x => x.CeremonyRequestId == requestId &&
                x.Status == CandidateInterviewAssignmentPolicy.Assigned &&
                x.NotificationQueuedAtUtc == null).ToArrayAsync(ct);
        var subjects = new Dictionary<Guid, string>();
        foreach (var row in rows)
        {
            var found = await db.Database.SqlQuery<string>($"""
                SELECT "Subject" AS "Value" FROM core.member_identity_links
                WHERE "MemberId" = {row.InterviewerMemberId} AND "RevokedAtUtc" IS NULL
                """).ToListAsync(ct);
            if (found.Count != 1) return Results.Conflict(new { message = "Identidad de Maestro no disponible." });
            subjects[row.InterviewerMemberId] = found[0];
        }
        var pending = await NotifyAsync(rows, subjects, service, db, ct);
        return Results.Ok(new { attempted = rows.Length, notificationsPending = pending });
    }

    private static async Task<Guid[]> NotifyAsync(
        IEnumerable<CandidateInterviewAssignment> rows,
        IReadOnlyDictionary<Guid, string> subjects,
        IInstitutionalNotificationService notifications, PmgmDbContext db, CancellationToken ct)
    {
        var pending = new List<Guid>();
        foreach (var row in rows)
        {
            try
            {
                await notifications.QueueAsync(new QueueNotificationCommand(
                    NotificationCodes.Template.CandidateInterviewAssigned, null,
                    NotificationCodes.Type.CandidateInterviewAssigned,
                    subjects[row.InterviewerMemberId], null,
                    new[] { NotificationCodes.Channel.Internal },
                    new Dictionary<string, string?> { ["workshop"] = "su Taller" },
                    $"candidate-interview-assignment:{row.Id:N}",
                    row.Id.ToString("N"), row.Id.ToString("N"),
                    "/interviews", true, null, true,
                    RelatedResourceType: "candidate_interview_assignment",
                    RelatedResourceId: row.Id.ToString("N")), ct);
                row.NotificationQueuedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                // La designación confirmada no se finge notificada: el Venerable puede reintentar.
                pending.Add(row.Id);
            }
        }
        return pending.ToArray();
    }

    private static async Task<IResult> DeliverAsync(
        Guid requestId, Guid assignmentId, CompleteCandidateInterview input, HttpContext http,
        PmgmDbContext db, DocumentManagementDbContext documents,
        IInstitutionalMemberContextResolver identity, IAuditService audit, CancellationToken ct)
    {
        var me = await identity.ResolveAsync(http.User, ct);
        if (me is null || me.EffectiveDegree < 3) return Results.Forbid();
        var assignment = await db.CandidateInterviewAssignments.SingleOrDefaultAsync(x =>
            x.Id == assignmentId && x.CeremonyRequestId == requestId &&
            x.InterviewerMemberId == me.MemberId, ct);
        if (assignment is null) return Results.NotFound();
        if (!await IsActiveDesignatedMasterAsync(db, assignment.OrganizationId, me.MemberId, ct))
            return Results.Forbid();
        if (assignment.Status != CandidateInterviewAssignmentPolicy.Assigned ||
            assignment.ReportDocumentVersionId is not null)
            return Results.Conflict(new { message = "La designación no está pendiente o ya fue entregada." });
        var valid = await documents.DocumentVersions.AsNoTracking()
            .Where(x => x.Id == input.DocumentVersionId &&
                x.DocumentId == assignmentId &&
                x.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available &&
                x.Document.OrganizationId == assignment.OrganizationId &&
                x.Document.Edition == requestId.ToString("N") &&
                x.Document.DocumentType == "candidate_interview")
            .Select(x => new
            {
                x.Document.DocumentDate, x.Document.AuthorName,
                x.Document.ShortDescription, x.Document.OfficialDocumentType
            }).SingleOrDefaultAsync(ct);
        if (valid is null || valid.DocumentDate is null ||
            valid.DocumentDate.Value < assignment.CouncilDecisionDate ||
            valid.DocumentDate.Value > Today() ||
            string.IsNullOrWhiteSpace(valid.ShortDescription) ||
            valid.OfficialDocumentType is not ("favorable" or "desfavorable"))
            return Results.Conflict(new { message = "El informe no cuenta con documento validado, fecha, resumen y resultado reglamentarios." });
        var official = await db.Members.AsNoTracking().Where(x => x.Id == me.MemberId)
            .Select(x => x.Person.FirstNames + " " + x.Person.LastNames)
            .SingleOrDefaultAsync(ct);
        if (!string.Equals(official?.Trim(), valid.AuthorName?.Trim(), StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(new { message = "El informe no identifica al Maestro designado." });
        assignment.ReportDocumentVersionId = input.DocumentVersionId;
        assignment.CompletedAtUtc = DateTimeOffset.UtcNow;
        assignment.Status = CandidateInterviewAssignmentPolicy.Completed;
        audit.Add(http, "candidate.interview.report.delivered",
            nameof(CandidateInterviewAssignment), assignment.Id.ToString(),
            assignment.OrganizationId, AuditResults.Success,
            new { assignment.CeremonyRequestId, assignment.InterviewerMemberId, input.DocumentVersionId });
        await db.SaveChangesAsync(ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { assignment.Id, assignment.Status, assignment.CompletedAtUtc });
    }

    private static async Task<IResult> MineAsync(
        HttpContext http, PmgmDbContext db,
        IInstitutionalMemberContextResolver identity, CancellationToken ct)
    {
        var me = await identity.ResolveAsync(http.User, ct);
        if (me is null || me.EffectiveDegree < 3) return Results.Forbid();
        var today = Today();
        var activeOrRecent = await db.CandidateInterviewAssignments.AsNoTracking()
            .Where(x => x.InterviewerMemberId == me.MemberId &&
                x.Status != CandidateInterviewAssignmentPolicy.Replaced &&
                db.Memberships.Any(m => m.MemberId == me.MemberId &&
                    m.OrganizationId == x.OrganizationId &&
                    m.Status == MembershipCodes.MembershipStatus.Active &&
                    (m.StartDate == null || m.StartDate <= today) &&
                    (m.EndDate == null || m.EndDate >= today)))
            .OrderByDescending(x => x.AssignedAtUtc)
            .Select(x => new
            {
                x.Id, x.CeremonyRequestId, x.OrganizationId, x.Position,
                x.ScheduledDate, x.Status, x.AssignedAtUtc,
                hasReport = x.ReportDocumentVersionId != null
            }).ToListAsync(ct);
        var name = await db.Members.AsNoTracking()
            .Where(x => x.Id == me.MemberId)
            .Select(x => x.Person.FirstNames + " " + x.Person.LastNames)
            .SingleOrDefaultAsync(ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            items = activeOrRecent.Select(x => new {
                x.Id, x.CeremonyRequestId, x.OrganizationId, x.Position,
                x.ScheduledDate, x.Status, x.AssignedAtUtc, x.hasReport,
                interviewerName = name
            })
        });
    }
}

public sealed record AssignCandidateInterviewers(
    IReadOnlyList<Guid> InterviewerMemberIds,
    string CouncilBody, DateOnly CouncilDecisionDate, string CouncilMinuteReference,
    IReadOnlyList<DateOnly?>? ScheduledDates = null, string? ReplacementReason = null);

public sealed record CompleteCandidateInterview(Guid DocumentVersionId);
