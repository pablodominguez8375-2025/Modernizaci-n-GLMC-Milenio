using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeCouncilEndpoints
{
    public static IEndpointRouteBuilder MapLodgeCouncilEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial")
            .WithTags("Consejo de Administración")
            .RequireAuthorization();

        group.MapPost("/talleres/{organizationId:guid}/consejo/sesiones", CreateSessionAsync);
        group.MapGet("/talleres/{organizationId:guid}/consejo/sesiones", GetSessionsAsync);
        group.MapGet("/consejo/sesiones/{sessionId:guid}", GetSessionAsync);
        group.MapPost("/consejo/sesiones/{sessionId:guid}/asistencia", RecordAttendanceAsync);
        group.MapGet("/consejo/sesiones/{sessionId:guid}/asistencia", GetAttendanceAsync);
        group.MapPost("/consejo/sesiones/{sessionId:guid}/confirmar-quorum", ConfirmQuorumAsync);
        group.MapPost("/consejo/sesiones/{sessionId:guid}/acuerdos", RecordDecisionAsync);
        group.MapGet("/consejo/sesiones/{sessionId:guid}/acuerdos", GetDecisionsAsync);
        group.MapPost("/consejo/sesiones/{sessionId:guid}/revisiones", RecordFinancialReviewAsync);
        group.MapGet("/consejo/sesiones/{sessionId:guid}/revisiones", GetFinancialReviewsAsync);
        group.MapPost("/consejo/sesiones/{sessionId:guid}/cerrar", CloseSessionAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateSessionAsync(
        Guid organizationId,
        CreateLodgeCouncilSessionRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManageCouncil(httpContext.User, organizationId, access)) return Results.Forbid();

        var organizationExists = await institutionalDb.Organizations.AsNoTracking()
            .AnyAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
        if (!organizationExists) return Results.NotFound(new { message = "El Taller indicado no existe." });

        var session = new LodgeCouncilSession
        {
            OrganizationId = organizationId,
            SessionDate = request.SessionDate,
            Title = NormalizeOptional(request.Title),
            Status = LodgeCouncilCodes.SessionStatus.Scheduled,
            QualifiedQuorumConfirmed = false,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeCouncilSessions.Add(session);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.council.session.created",
            nameof(LodgeCouncilSession),
            session.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { session.SessionDate, session.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/consejo/sesiones/{session.Id}", ToSessionDto(session));
    }

    private static async Task<IResult> GetSessionsAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanParticipateInLodgeCouncil(httpContext.User, organizationId)) return Results.Forbid();
        if (from is not null && to is not null && to < from)
            return Results.BadRequest(new { message = "La fecha final no puede ser anterior a la fecha inicial." });

        var query = db.LodgeCouncilSessions.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (from is not null) query = query.Where(x => x.SessionDate >= from.Value);
        if (to is not null) query = query.Where(x => x.SessionDate <= to.Value);

        var items = await query
            .OrderByDescending(x => x.SessionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(120)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeCouncilSessionsResponse(items.Count, items.Select(ToSessionDto).ToList()));
    }

    private static async Task<IResult> GetSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!access.CanParticipateInLodgeCouncil(httpContext.User, session.OrganizationId)) return Results.Forbid();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(ToSessionDto(session));
    }

    private static async Task<IResult> RecordAttendanceAsync(
        Guid sessionId,
        LodgeCouncilAttendanceRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!CanManageCouncil(httpContext.User, session.OrganizationId, access)) return Results.Forbid();
        if (session.Status != LodgeCouncilCodes.SessionStatus.Scheduled)
            return Results.Conflict(new { message = "La asistencia sólo puede modificarse mientras la sesión está programada." });
        if (!LodgeCouncilCodes.ParticipationType.IsValid(request.ParticipationType) ||
            !LodgeCouncilCodes.AttendanceStatus.IsValid(request.Status))
            return Results.BadRequest(new { message = "El tipo de participación o estado de asistencia no es válido." });

        Guid? memberId = null;
        string displayName;
        string? institutionalRole = null;
        var hasVoice = true;
        var hasVote = false;

        if (request.ParticipationType == LodgeCouncilCodes.ParticipationType.Member)
        {
            if (request.MemberId is null || !LodgeCouncilPolicy.IsCouncilRole(request.InstitutionalRole))
                return Results.BadRequest(new { message = "Un integrante del Consejo debe indicar hermano y cargo institucional válido." });

            var member = await institutionalDb.Memberships.AsNoTracking()
                .Where(x => x.MemberId == request.MemberId.Value &&
                            x.OrganizationId == session.OrganizationId &&
                            x.EndDate == null &&
                            x.Status == MembershipCodes.MembershipStatus.Active)
                .Select(x => new
                {
                    x.MemberId,
                    DisplayName = (x.Member.Person.FirstNames + " " + x.Member.Person.LastNames).Trim()
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (member is null)
                return Results.BadRequest(new { message = "El integrante no registra pertenencia activa al Taller." });

            var effectiveOfficeTypes = await institutionalDb.OfficeAssignments.AsNoTracking()
                .Where(x => x.MemberId == member.MemberId &&
                            x.OrganizationId == session.OrganizationId &&
                            x.StartDate <= session.SessionDate &&
                            (x.EndDate == null || x.EndDate >= session.SessionDate))
                .Select(x => x.OfficeType)
                .ToListAsync(cancellationToken);

            if (!effectiveOfficeTypes.Any(x => LodgeCouncilPolicy.OfficeTypeMatchesRole(x, request.InstitutionalRole)))
                return Results.BadRequest(new
                {
                    message = "El integrante no registra una asignación vigente para el cargo del Consejo indicado en la fecha de la sesión."
                });

            memberId = member.MemberId;
            displayName = member.DisplayName;
            institutionalRole = request.InstitutionalRole;
            hasVote = LodgeCouncilPolicy.CanVote(request.ParticipationType, institutionalRole);
        }
        else
        {
            displayName = NormalizeOptional(request.DisplayName) ?? string.Empty;
            if (displayName.Length < 3)
                return Results.BadRequest(new { message = "El invitado debe indicar un nombre identificable." });
            hasVoice = request.HasVoice ?? true;
        }

        var record = new LodgeCouncilAttendanceRecord
        {
            SessionId = session.Id,
            MemberId = memberId,
            DisplayName = displayName,
            InstitutionalRole = institutionalRole,
            ParticipationType = request.ParticipationType,
            Status = request.Status,
            HasVoice = hasVoice,
            HasVote = hasVote,
            RecordedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeCouncilAttendanceRecords.Add(record);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.council.attendance.recorded",
            nameof(LodgeCouncilAttendanceRecord),
            record.Id.ToString(),
            session.OrganizationId,
            AuditResults.Success,
            new { sessionId, record.MemberId, record.InstitutionalRole, record.ParticipationType, record.Status, record.HasVoice, record.HasVote }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/consejo/sesiones/{sessionId}/asistencia/{record.Id}", ToAttendanceDto(record));
    }

    private static async Task<IResult> GetAttendanceAsync(
        Guid sessionId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!access.CanParticipateInLodgeCouncil(httpContext.User, session.OrganizationId)) return Results.Forbid();

        var rows = await db.LodgeCouncilAttendanceRecords.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var memberRows = rows
            .Where(x => x.MemberId is not null)
            .GroupBy(x => x.MemberId)
            .Select(x => x.First());
        var guestRows = rows.Where(x => x.MemberId is null);
        var items = memberRows.Concat(guestRows)
            .OrderBy(x => x.DisplayName)
            .Select(ToAttendanceDto)
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeCouncilAttendanceResponse(items.Count, items));
    }

    private static async Task<IResult> ConfirmQuorumAsync(
        Guid sessionId,
        ConfirmLodgeCouncilQuorumRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!CanManageCouncil(httpContext.User, session.OrganizationId, access)) return Results.Forbid();
        if (session.Status != LodgeCouncilCodes.SessionStatus.Scheduled)
            return Results.Conflict(new { message = "El quórum sólo puede confirmarse en una sesión abierta a registro." });

        var presentVotingMembers = await GetPresentVotingMemberCountAsync(sessionId, db, cancellationToken);
        if (!LodgeCouncilPolicy.CanConfirmQualifiedQuorum(request.Confirmed, presentVotingMembers))
            return Results.BadRequest(new { message = "La confirmación requiere declaración expresa y al menos un integrante presente con derecho a voto. El umbral de quórum calificado se rige por la definición institucional vigente." });

        session.QualifiedQuorumConfirmed = true;
        session.QuorumConfirmedBySubject = GetSubject(httpContext.User);
        session.QuorumConfirmedAtUtc = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.council.quorum.confirmed",
            nameof(LodgeCouncilSession),
            session.Id.ToString(),
            session.OrganizationId,
            AuditResults.Success,
            new { presentVotingMembers, qualification = "institutional_confirmation", numericThresholdHardcoded = false }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new LodgeCouncilQuorumResponse(session.Id, true, presentVotingMembers, session.QuorumConfirmedAtUtc));
    }

    private static async Task<IResult> RecordDecisionAsync(
        Guid sessionId,
        LodgeCouncilDecisionRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!CanManageCouncil(httpContext.User, session.OrganizationId, access)) return Results.Forbid();
        if (session.Status != LodgeCouncilCodes.SessionStatus.Scheduled)
            return Results.Conflict(new { message = "No se pueden registrar acuerdos en una sesión cerrada o cancelada." });
        if (!session.QualifiedQuorumConfirmed)
            return Results.Conflict(new { message = "Debe registrarse la confirmación institucional de quórum calificado antes de adoptar acuerdos." });
        if (!LodgeCouncilCodes.DecisionCategory.IsValid(request.Category) ||
            !LodgeCouncilPolicy.IsOutcomeCompatible(request.Category, request.Outcome))
            return Results.BadRequest(new { message = "La categoría o el resultado no es compatible con la competencia normativa del Consejo." });
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Resolution))
            return Results.BadRequest(new { message = "El asunto y la resolución son obligatorios." });
        if (request.Amount is < 0)
            return Results.BadRequest(new { message = "El monto no puede ser negativo." });

        var requiresChamberReview = LodgeCouncilPolicy.RequiresChamberReview(request.Category);
        var decision = new LodgeCouncilDecision
        {
            SessionId = session.Id,
            Category = request.Category,
            Subject = request.Subject.Trim(),
            Resolution = request.Resolution.Trim(),
            Outcome = request.Outcome,
            RequiresChamberReview = requiresChamberReview,
            ChamberReference = NormalizeOptional(request.ChamberReference),
            SupportingDocumentId = request.SupportingDocumentId,
            Amount = request.Amount,
            RecordedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeCouncilDecisions.Add(decision);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.council.decision.recorded",
            nameof(LodgeCouncilDecision),
            decision.Id.ToString(),
            session.OrganizationId,
            AuditResults.Success,
            new { sessionId, decision.Category, decision.Outcome, decision.RequiresChamberReview, decision.SupportingDocumentId, decision.Amount }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/consejo/sesiones/{sessionId}/acuerdos/{decision.Id}", ToDecisionDto(decision));
    }

    private static async Task<IResult> GetDecisionsAsync(
        Guid sessionId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!access.CanParticipateInLodgeCouncil(httpContext.User, session.OrganizationId)) return Results.Forbid();

        var items = await db.LodgeCouncilDecisions.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeCouncilDecisionsResponse(items.Count, items.Select(ToDecisionDto).ToList()));
    }

    private static async Task<IResult> RecordFinancialReviewAsync(
        Guid sessionId,
        LodgeCouncilFinancialReviewRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!CanManageCouncil(httpContext.User, session.OrganizationId, access)) return Results.Forbid();
        if (session.Status != LodgeCouncilCodes.SessionStatus.Scheduled)
            return Results.Conflict(new { message = "La revisión sólo puede registrarse mientras la sesión está abierta a registro." });
        if (!LodgeCouncilCodes.ControlArea.IsValid(request.ControlArea) ||
            string.IsNullOrWhiteSpace(request.PeriodLabel) ||
            string.IsNullOrWhiteSpace(request.Conclusion))
            return Results.BadRequest(new { message = "Área, período y conclusión son obligatorios." });

        var review = new LodgeCouncilFinancialReview
        {
            SessionId = session.Id,
            ControlArea = request.ControlArea,
            PeriodLabel = request.PeriodLabel.Trim(),
            Conclusion = request.Conclusion.Trim(),
            Observations = NormalizeOptional(request.Observations),
            SupportingDocumentId = request.SupportingDocumentId,
            RecordedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeCouncilFinancialReviews.Add(review);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.council.review.recorded",
            nameof(LodgeCouncilFinancialReview),
            review.Id.ToString(),
            session.OrganizationId,
            AuditResults.Success,
            new { sessionId, review.ControlArea, review.PeriodLabel, review.SupportingDocumentId }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/consejo/sesiones/{sessionId}/revisiones/{review.Id}", ToReviewDto(review));
    }

    private static async Task<IResult> GetFinancialReviewsAsync(
        Guid sessionId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!access.CanParticipateInLodgeCouncil(httpContext.User, session.OrganizationId)) return Results.Forbid();

        var items = await db.LodgeCouncilFinancialReviews.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeCouncilFinancialReviewsResponse(items.Count, items.Select(ToReviewDto).ToList()));
    }

    private static async Task<IResult> CloseSessionAsync(
        Guid sessionId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var session = await db.LodgeCouncilSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return Results.NotFound();
        if (!CanManageCouncil(httpContext.User, session.OrganizationId, access)) return Results.Forbid();
        if (session.Status == LodgeCouncilCodes.SessionStatus.Closed)
            return Results.Conflict(new { message = "La sesión ya está cerrada." });
        if (session.Status == LodgeCouncilCodes.SessionStatus.Cancelled)
            return Results.Conflict(new { message = "Una sesión cancelada no puede cerrarse." });
        if (!session.QualifiedQuorumConfirmed)
            return Results.Conflict(new { message = "No se puede cerrar como sesión válida sin confirmación institucional del quórum calificado." });

        session.Status = LodgeCouncilCodes.SessionStatus.Closed;
        session.ClosedAtUtc = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.council.session.closed",
            nameof(LodgeCouncilSession),
            session.Id.ToString(),
            session.OrganizationId,
            AuditResults.Success,
            new { session.SessionDate, session.QualifiedQuorumConfirmed, session.ClosedAtUtc }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToSessionDto(session));
    }

    private static async Task<int> GetPresentVotingMemberCountAsync(
        Guid sessionId,
        LodgeManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.LodgeCouncilAttendanceRecords.AsNoTracking()
            .Where(x => x.SessionId == sessionId && x.MemberId != null)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows.GroupBy(x => x.MemberId)
            .Select(x => x.First())
            .Count(x => x.Status == LodgeCouncilCodes.AttendanceStatus.Present && x.HasVote);
    }

    private static bool CanManageCouncil(ClaimsPrincipal user, Guid organizationId, IInstitutionalAccessService access)
        => access.CanParticipateInLodgeCouncil(user, organizationId) &&
           access.HasRole(user, InstitutionalRoles.TallerVenerable, InstitutionalRoles.TallerSecretaria);

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.Identity?.Name ?? "unknown";

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LodgeCouncilSessionDto ToSessionDto(LodgeCouncilSession x)
        => new(x.Id, x.OrganizationId, x.SessionDate, x.Title, x.Status, x.QualifiedQuorumConfirmed,
            x.QuorumConfirmedBySubject, x.QuorumConfirmedAtUtc, x.CreatedAtUtc, x.ClosedAtUtc);

    private static LodgeCouncilAttendanceDto ToAttendanceDto(LodgeCouncilAttendanceRecord x)
        => new(x.Id, x.SessionId, x.MemberId, x.DisplayName, x.InstitutionalRole, x.ParticipationType,
            x.Status, x.HasVoice, x.HasVote, x.RecordedAtUtc);

    private static LodgeCouncilDecisionDto ToDecisionDto(LodgeCouncilDecision x)
        => new(x.Id, x.SessionId, x.Category, x.Subject, x.Resolution, x.Outcome, x.RequiresChamberReview,
            x.ChamberReference, x.SupportingDocumentId, x.Amount, x.RecordedAtUtc);

    private static LodgeCouncilFinancialReviewDto ToReviewDto(LodgeCouncilFinancialReview x)
        => new(x.Id, x.SessionId, x.ControlArea, x.PeriodLabel, x.Conclusion, x.Observations,
            x.SupportingDocumentId, x.RecordedAtUtc);
}

public sealed record CreateLodgeCouncilSessionRequest(DateOnly SessionDate, string? Title);
public sealed record LodgeCouncilAttendanceRequest(Guid? MemberId, string? DisplayName, string? InstitutionalRole, string ParticipationType, string Status, bool? HasVoice);
public sealed record ConfirmLodgeCouncilQuorumRequest(bool Confirmed);
public sealed record LodgeCouncilDecisionRequest(string Category, string Subject, string Resolution, string Outcome, string? ChamberReference, Guid? SupportingDocumentId, decimal? Amount);
public sealed record LodgeCouncilFinancialReviewRequest(string ControlArea, string PeriodLabel, string Conclusion, string? Observations, Guid? SupportingDocumentId);

public sealed record LodgeCouncilSessionDto(Guid Id, Guid OrganizationId, DateOnly SessionDate, string? Title, string Status, bool QualifiedQuorumConfirmed, string? QuorumConfirmedBySubject, DateTimeOffset? QuorumConfirmedAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? ClosedAtUtc);
public sealed record LodgeCouncilSessionsResponse(int Total, IReadOnlyList<LodgeCouncilSessionDto> Items);
public sealed record LodgeCouncilAttendanceDto(Guid Id, Guid SessionId, Guid? MemberId, string DisplayName, string? InstitutionalRole, string ParticipationType, string Status, bool HasVoice, bool HasVote, DateTimeOffset RecordedAtUtc);
public sealed record LodgeCouncilAttendanceResponse(int Total, IReadOnlyList<LodgeCouncilAttendanceDto> Items);
public sealed record LodgeCouncilQuorumResponse(Guid SessionId, bool Confirmed, int PresentVotingMembers, DateTimeOffset? ConfirmedAtUtc);
public sealed record LodgeCouncilDecisionDto(Guid Id, Guid SessionId, string Category, string Subject, string Resolution, string Outcome, bool RequiresChamberReview, string? ChamberReference, Guid? SupportingDocumentId, decimal? Amount, DateTimeOffset RecordedAtUtc);
public sealed record LodgeCouncilDecisionsResponse(int Total, IReadOnlyList<LodgeCouncilDecisionDto> Items);
public sealed record LodgeCouncilFinancialReviewDto(Guid Id, Guid SessionId, string ControlArea, string PeriodLabel, string Conclusion, string? Observations, Guid? SupportingDocumentId, DateTimeOffset RecordedAtUtc);
public sealed record LodgeCouncilFinancialReviewsResponse(int Total, IReadOnlyList<LodgeCouncilFinancialReviewDto> Items);
