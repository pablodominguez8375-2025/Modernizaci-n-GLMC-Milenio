using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;
using PMGM.Api.Modules.SecretariatOperations.Entities;

namespace PMGM.Api.Modules.SecretariatOperations;

public static class SecretariatOperationsEndpoints
{
    private static readonly HashSet<string> CouncilOfficeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        InstitutionalRoles.TallerVenerable,
        InstitutionalRoles.TallerInmediatoExVenerable,
        InstitutionalRoles.TallerPrimerVigilante,
        InstitutionalRoles.TallerSegundoVigilante,
        InstitutionalRoles.TallerOrador,
        InstitutionalRoles.TallerSecretaria,
        InstitutionalRoles.TallerTesoreria,
        InstitutionalRoles.TallerHospitalaria
    };

    public static IEndpointRouteBuilder MapSecretariatOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/secretaria")
            .WithTags("Secretaría")
            .RequireAuthorization();

        group.MapGet("/talleres/{organizationId:guid}/cuadro/carga-historica", ListHistoricalIntakesAsync);
        group.MapPost("/talleres/{organizationId:guid}/cuadro/carga-historica", CreateHistoricalIntakeAsync);
        group.MapGet("/talleres/{organizationId:guid}/cuadro/plantilla.xlsx", DownloadHistoricalTemplateAsync);
        group.MapPost("/talleres/{organizationId:guid}/cuadro/importar.xlsx", ImportHistoricalIntakesAsync).DisableAntiforgery();
        group.MapPost("/carga-historica/{intakeId:guid}/enviar-ri", SubmitHistoricalIntakeAsync);

        group.MapGet("/reuniones/talleres/{organizationId:guid}", ListAdministrativeMeetingsAsync);
        group.MapPost("/reuniones/talleres/{organizationId:guid}", CreateAdministrativeMeetingAsync);
        group.MapPost("/reuniones/{meetingId:guid}/realizar", MarkAdministrativeMeetingHeldAsync);

        group.MapGet("/talleres/{organizationId:guid}/autorizaciones-ceremonia", ListCeremonyAuthorizationsAsync);
        group.MapGet("/talleres/{organizationId:guid}/registros", ListLodgeRecordsAsync);
        group.MapPut("/talleres/{organizationId:guid}/registros/{recordType}/{sourceRecordId:guid}", UpsertLodgeRecordAsync);
        group.MapPost("/talleres/{organizationId:guid}/tenidas/{meetingId:guid}/remitir-extracto", SubmitTenidaExtractAsync);

        var ri = endpoints.MapGroup("/api/regimen-interior/carga-historica")
            .WithTags("Régimen Interior")
            .RequireAuthorization();

        ri.MapGet("/", ListPendingHistoricalIntakesAsync);
        ri.MapPost("/{intakeId:guid}/resolver", ReviewHistoricalIntakeAsync);

        return endpoints;
    }

    private static async Task<IResult> ListHistoricalIntakesAsync(
        Guid organizationId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();

        var items = await db.HistoricalMemberIntakes.AsNoTracking()
            .Include(x => x.Offices)
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => ToHistoricalIntakeDto(x))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> CreateHistoricalIntakeAsync(
        Guid organizationId,
        HistoricalMemberIntakeRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();

        var validation = ValidateIntakeRequest(request);
        if (validation is not null) return Results.BadRequest(new { message = validation });

        var exists = await db.Organizations.AsNoTracking()
            .AnyAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
        if (!exists) return Results.NotFound(new { message = "El Taller indicado no existe." });

        var rut = SecretariatOperationsPolicy.NormalizeRut(request.Rut);
        var targetMemberId = await FindExistingMemberAsync(db, organizationId, rut, NormalizeOptional(request.InstitutionalNumber), cancellationToken);

        var entity = new HistoricalMemberIntake
        {
            OrganizationId = organizationId,
            TargetMemberId = targetMemberId,
            CutoffDate = request.CutoffDate,
            FirstNames = request.FirstNames.Trim(),
            LastNames = request.LastNames.Trim(),
            Rut = rut,
            InstitutionalNumber = NormalizeOptional(request.InstitutionalNumber),
            Email = NormalizeOptional(request.Email),
            Phone = NormalizeOptional(request.Phone),
            CurrentDegree = request.CurrentDegree,
            MembershipStartDate = request.MembershipStartDate,
            InitiationDate = request.InitiationDate,
            WageIncreaseDate = request.WageIncreaseDate,
            ExaltationDate = request.ExaltationDate,
            EvidenceReference = request.EvidenceReference.Trim(),
            Status = SecretariatOperationsCodes.IntakeStatus.Draft,
            CreatedBySubject = GetSubject(httpContext.User),
            Offices = request.Offices.Select(x => new HistoricalMemberIntakeOffice
            {
                OfficeType = x.OfficeType.Trim(),
                Period = x.Period.Trim(),
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                IsCurrent = x.IsCurrent
            }).ToList()
        };

        db.HistoricalMemberIntakes.Add(entity);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "secretariat.historical_member_intake.created",
            nameof(HistoricalMemberIntake),
            entity.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { entity.TargetMemberId, entity.CurrentDegree, officeCount = entity.Offices.Count }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/secretaria/carga-historica/{entity.Id}", ToHistoricalIntakeDto(entity));
    }

    private static IResult DownloadHistoricalTemplateAsync(
        Guid organizationId,
        HttpContext httpContext,
        IInstitutionalAccessService access)
    {
        if (!access.CanManageLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();

        using var workbook = new XLWorkbook();
        var brothers = workbook.AddWorksheet("Hermanos");
        var headers = new[]
        {
            "clave","nombres","apellidos","rut","numero_institucional","email","celular","grado_actual",
            "fecha_ingreso_taller","fecha_iniciacion","fecha_aumento_salario","fecha_exaltacion",
            "fecha_corte","fuente_evidencia"
        };
        for (var i = 0; i < headers.Length; i++) brothers.Cell(1, i + 1).Value = headers[i];
        brothers.Cell(2, 1).Value = "H001";
        brothers.Cell(2, 2).Value = "Nombre";
        brothers.Cell(2, 3).Value = "Apellidos";
        brothers.Cell(2, 8).Value = SecretariatOperationsCodes.CurrentDegree.Master;
        brothers.Cell(2, 13).Value = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        brothers.Cell(2, 14).Value = "Cuadro del Taller / acta / decreto / libro de Secretaría";
        brothers.SheetView.FreezeRows(1);
        brothers.Columns().AdjustToContents();

        var offices = workbook.AddWorksheet("Cargos");
        var officeHeaders = new[] { "clave","cargo","periodo","fecha_inicio","fecha_termino","vigente" };
        for (var i = 0; i < officeHeaders.Length; i++) offices.Cell(1, i + 1).Value = officeHeaders[i];
        offices.Cell(2, 1).Value = "H001";
        offices.Cell(2, 2).Value = InstitutionalRoles.TallerSecretaria;
        offices.Cell(2, 3).Value = "2026";
        offices.Cell(2, 6).Value = true;
        offices.SheetView.FreezeRows(1);
        offices.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Results.File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Cuadro-del-Taller-carga-inicial-{organizationId:N}.xlsx");
    }

    private static async Task<IResult> ImportHistoricalIntakesAsync(
        Guid organizationId,
        HttpRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();
        if (!request.HasFormContentType) return Results.BadRequest(new { message = "Debe adjuntar una plantilla XLSX." });

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0) return Results.BadRequest(new { message = "Falta el archivo XLSX." });
        if (file.Length > 10 * 1024 * 1024) return Results.BadRequest(new { message = "La plantilla supera el máximo de 10 MB." });
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { message = "La importación masiva requiere formato .xlsx." });

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        XLWorkbook workbook;
        try { workbook = new XLWorkbook(memory); }
        catch { return Results.BadRequest(new { message = "El XLSX no pudo abrirse o está dañado." }); }

        using (workbook)
        {
            if (!workbook.TryGetWorksheet("Hermanos", out var brothers) || !workbook.TryGetWorksheet("Cargos", out var officesSheet))
                return Results.BadRequest(new { message = "La plantilla debe contener las hojas Hermanos y Cargos." });

            var officeMap = ParseOfficeRows(officesSheet);
            var drafts = new List<HistoricalMemberIntake>();
            var errors = new List<object>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in brothers.RowsUsed().Skip(1))
            {
                var key = row.Cell(1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!keys.Add(key))
                {
                    errors.Add(new { row = row.RowNumber(), key, message = "Clave duplicada en Hermanos." });
                    continue;
                }

                var intakeRequest = ParseIntakeRow(row, officeMap.GetValueOrDefault(key, []));
                var validation = ValidateIntakeRequest(intakeRequest);
                if (validation is not null)
                {
                    errors.Add(new { row = row.RowNumber(), key, message = validation });
                    continue;
                }

                var rut = SecretariatOperationsPolicy.NormalizeRut(intakeRequest.Rut);
                var target = await FindExistingMemberAsync(db, organizationId, rut, NormalizeOptional(intakeRequest.InstitutionalNumber), cancellationToken);
                drafts.Add(new HistoricalMemberIntake
                {
                    OrganizationId = organizationId,
                    TargetMemberId = target,
                    CutoffDate = intakeRequest.CutoffDate,
                    FirstNames = intakeRequest.FirstNames.Trim(),
                    LastNames = intakeRequest.LastNames.Trim(),
                    Rut = rut,
                    InstitutionalNumber = NormalizeOptional(intakeRequest.InstitutionalNumber),
                    Email = NormalizeOptional(intakeRequest.Email),
                    Phone = NormalizeOptional(intakeRequest.Phone),
                    CurrentDegree = intakeRequest.CurrentDegree,
                    MembershipStartDate = intakeRequest.MembershipStartDate,
                    InitiationDate = intakeRequest.InitiationDate,
                    WageIncreaseDate = intakeRequest.WageIncreaseDate,
                    ExaltationDate = intakeRequest.ExaltationDate,
                    EvidenceReference = intakeRequest.EvidenceReference.Trim(),
                    Status = SecretariatOperationsCodes.IntakeStatus.Draft,
                    CreatedBySubject = GetSubject(httpContext.User),
                    Offices = intakeRequest.Offices.Select(x => new HistoricalMemberIntakeOffice
                    {
                        OfficeType = x.OfficeType.Trim(),
                        Period = x.Period.Trim(),
                        StartDate = x.StartDate,
                        EndDate = x.EndDate,
                        IsCurrent = x.IsCurrent
                    }).ToList()
                });
            }

            if (errors.Count > 0)
                return Results.BadRequest(new { message = "La plantilla contiene errores; no se importó ninguna fila.", errors });

            db.HistoricalMemberIntakes.AddRange(drafts);
            db.AuditEvents.Add(AuditEventFactory.Create(
                httpContext,
                "secretariat.historical_member_intake.bulk_imported",
                nameof(HistoricalMemberIntake),
                Guid.Empty.ToString(),
                organizationId,
                AuditResults.Success,
                new { fileName = file.FileName, count = drafts.Count }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { imported = drafts.Count, status = SecretariatOperationsCodes.IntakeStatus.Draft });
        }
    }

    private static async Task<IResult> SubmitHistoricalIntakeAsync(
        Guid intakeId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var intake = await db.HistoricalMemberIntakes.Include(x => x.Offices)
            .SingleOrDefaultAsync(x => x.Id == intakeId, cancellationToken);
        if (intake is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(httpContext.User, intake.OrganizationId)) return Results.Forbid();
        if (!SecretariatOperationsCodes.IntakeStatus.IsEditable(intake.Status))
            return Results.Conflict(new { message = "La carga histórica no se encuentra editable." });

        intake.Status = SecretariatOperationsCodes.IntakeStatus.Submitted;
        intake.SubmittedAtUtc = DateTimeOffset.UtcNow;
        intake.Revision += 1;
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext, "secretariat.historical_member_intake.submitted",
            nameof(HistoricalMemberIntake), intake.Id.ToString(), intake.OrganizationId,
            AuditResults.Success, new { intake.Revision }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToHistoricalIntakeDto(intake));
    }

    private static async Task<IResult> ListPendingHistoricalIntakesAsync(
        Guid? organizationId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReviewHistoricalMemberIntake(httpContext.User)) return Results.Forbid();

        var query = db.HistoricalMemberIntakes.AsNoTracking()
            .Include(x => x.Offices)
            .Where(x => x.Status == SecretariatOperationsCodes.IntakeStatus.Submitted);
        if (organizationId is not null) query = query.Where(x => x.OrganizationId == organizationId);

        var items = await query.OrderBy(x => x.SubmittedAtUtc).Take(1000)
            .Select(x => ToHistoricalIntakeDto(x))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> ReviewHistoricalIntakeAsync(
        Guid intakeId,
        HistoricalMemberReviewRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReviewHistoricalMemberIntake(httpContext.User)) return Results.Forbid();
        if (request.Decision is not "approve" and not "observe" and not "reject")
            return Results.BadRequest(new { message = "La decisión debe ser approve, observe o reject." });

        var intake = await db.HistoricalMemberIntakes.Include(x => x.Offices)
            .SingleOrDefaultAsync(x => x.Id == intakeId, cancellationToken);
        if (intake is null) return Results.NotFound();
        if (intake.Status != SecretariatOperationsCodes.IntakeStatus.Submitted)
            return Results.Conflict(new { message = "Sólo una carga enviada a Régimen Interior puede resolverse." });

        if (request.Decision is "observe" or "reject" && string.IsNullOrWhiteSpace(request.Notes))
            return Results.BadRequest(new { message = "La observación o rechazo debe indicar el motivo." });

        intake.ReviewedBySubject = GetSubject(httpContext.User);
        intake.ReviewedAtUtc = DateTimeOffset.UtcNow;
        intake.ReviewNotes = NormalizeOptional(request.Notes);

        if (request.Decision == "observe")
        {
            intake.Status = SecretariatOperationsCodes.IntakeStatus.Observed;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToHistoricalIntakeDto(intake));
        }

        if (request.Decision == "reject")
        {
            intake.Status = SecretariatOperationsCodes.IntakeStatus.Rejected;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToHistoricalIntakeDto(intake));
        }

        await ApplyApprovedIntakeAsync(db, intake, cancellationToken);
        intake.Status = SecretariatOperationsCodes.IntakeStatus.Approved;
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext, "regimen_interior.historical_member_intake.approved",
            nameof(HistoricalMemberIntake), intake.Id.ToString(), intake.OrganizationId,
            AuditResults.Success, new { intake.ApprovedMemberId, intake.CurrentDegree, officeCount = intake.Offices.Count }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToHistoricalIntakeDto(intake));
    }

    private static async Task ApplyApprovedIntakeAsync(
        PmgmDbContext db,
        HistoricalMemberIntake intake,
        CancellationToken cancellationToken)
    {
        Member member;
        if (intake.TargetMemberId is not null)
        {
            member = await db.Members.Include(x => x.Person)
                .SingleAsync(x => x.Id == intake.TargetMemberId.Value, cancellationToken);
            member.Person.FirstNames = intake.FirstNames;
            member.Person.LastNames = intake.LastNames;
            member.Person.Rut = intake.Rut;
            member.Person.Email = intake.Email;
            member.Person.Phone = intake.Phone;
            member.InstitutionalNumber = intake.InstitutionalNumber ?? member.InstitutionalNumber;
            member.CurrentDegree = intake.CurrentDegree;
        }
        else
        {
            var person = new Person
            {
                FirstNames = intake.FirstNames,
                LastNames = intake.LastNames,
                Rut = intake.Rut,
                Email = intake.Email,
                Phone = intake.Phone
            };
            member = new Member
            {
                Person = person,
                InstitutionalNumber = intake.InstitutionalNumber,
                CurrentDegree = intake.CurrentDegree,
            };
            db.Members.Add(member);
        }

        var membership = await db.Memberships
            .SingleOrDefaultAsync(
                x => x.MemberId == member.Id && x.OrganizationId == intake.OrganizationId && x.EndDate == null,
                cancellationToken);

        if (membership is null)
        {
            membership = new MembershipEntity
            {
                Member = member,
                OrganizationId = intake.OrganizationId,
                MembershipType = "regular",
                StartDate = intake.MembershipStartDate,
                Status = MembershipCodes.MembershipStatus.Active,
            };
            db.Memberships.Add(membership);
        }
        else
        {
            membership.StartDate = intake.MembershipStartDate ?? membership.StartDate;
            membership.Status = MembershipCodes.MembershipStatus.Active;
        }

        if (intake.InitiationDate is not null)
            await AddDegreeEventIfMissingAsync(db, member.Id, intake.OrganizationId, MembershipCodes.DegreeEvent.Initiation, intake.InitiationDate.Value, SecretariatOperationsCodes.CurrentDegree.Apprentice, intake.EvidenceReference, cancellationToken);
        if (intake.WageIncreaseDate is not null)
            await AddDegreeEventIfMissingAsync(db, member.Id, intake.OrganizationId, MembershipCodes.DegreeEvent.WageIncrease, intake.WageIncreaseDate.Value, SecretariatOperationsCodes.CurrentDegree.Fellowcraft, intake.EvidenceReference, cancellationToken);
        if (intake.ExaltationDate is not null)
            await AddDegreeEventIfMissingAsync(db, member.Id, intake.OrganizationId, MembershipCodes.DegreeEvent.Exaltation, intake.ExaltationDate.Value, SecretariatOperationsCodes.CurrentDegree.Master, intake.EvidenceReference, cancellationToken);

        foreach (var office in intake.Offices)
        {
            var duplicate = await db.OfficeAssignments.AnyAsync(
                x => x.MemberId == member.Id &&
                     x.OrganizationId == intake.OrganizationId &&
                     x.OfficeType == office.OfficeType &&
                     x.Period == office.Period,
                cancellationToken);
            if (duplicate) continue;

            db.OfficeAssignments.Add(new OfficeAssignment
            {
                Member = member,
                OrganizationId = intake.OrganizationId,
                OfficeType = office.OfficeType,
                Period = office.Period,
                StartDate = office.StartDate,
                EndDate = office.IsCurrent ? null : office.EndDate,
                EvidenceReference = intake.EvidenceReference
            });
        }

        intake.ApprovedMemberId = member.Id;
    }

    private static async Task AddDegreeEventIfMissingAsync(
        PmgmDbContext db,
        Guid memberId,
        Guid organizationId,
        string eventType,
        DateOnly eventDate,
        string resultingDegree,
        string evidence,
        CancellationToken cancellationToken)
    {
        var exists = await db.DegreeEvents.AnyAsync(
            x => x.MemberId == memberId && x.EventType == eventType && x.EffectiveDate == eventDate,
            cancellationToken);
        if (exists) return;

        db.DegreeEvents.Add(new DegreeEvent
        {
            MemberId = memberId,
            OrganizationId = organizationId,
            Degree = resultingDegree,
            EventType = eventType,
            EffectiveDate = eventDate,
            EvidenceReference = evidence
        });
    }

    private static async Task<IResult> ListAdministrativeMeetingsAsync(
        Guid organizationId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();
        var items = await db.LodgeAdministrativeMeetings.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.MeetingDate)
            .Take(250)
            .ToListAsync(cancellationToken);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> CreateAdministrativeMeetingAsync(
        Guid organizationId,
        CreateAdministrativeMeetingRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { message = "La reunión requiere un título." });

        var entity = new LodgeAdministrativeMeeting
        {
            OrganizationId = organizationId,
            MeetingDate = request.MeetingDate,
            Title = request.Title.Trim(),
            Purpose = NormalizeOptional(request.Purpose),
            Status = SecretariatOperationsCodes.AdministrativeMeetingStatus.Scheduled,
            CreatedBySubject = GetSubject(httpContext.User)
        };
        db.LodgeAdministrativeMeetings.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/secretaria/reuniones/{entity.Id}", entity);
    }

    private static async Task<IResult> MarkAdministrativeMeetingHeldAsync(
        Guid meetingId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var entity = await db.LodgeAdministrativeMeetings.SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (entity is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(httpContext.User, entity.OrganizationId)) return Results.Forbid();
        entity.Status = SecretariatOperationsCodes.AdministrativeMeetingStatus.Held;
        entity.HeldAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(entity);
    }

    private static async Task<IResult> ListCeremonyAuthorizationsAsync(
        Guid organizationId,
        HttpContext httpContext,
        PmgmDbContext db,
        GrandSecretariatDbContext grandSecretariatDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();

        var documents = await grandSecretariatDb.SecretariatDocuments.AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.Status == GrandSecretariatCodes.DocumentStatus.Issued &&
                x.RelatedCeremonyRequestId != null &&
                ((x.DocumentType == GrandSecretariatCodes.DocumentType.Plancha &&
                  x.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization) ||
                 x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationLegacy ||
                 x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationPlanchaLegacy))
            .OrderByDescending(x => x.IssuedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var requestIds = documents
            .Where(x => x.RelatedCeremonyRequestId is not null)
            .Select(x => x.RelatedCeremonyRequestId!.Value)
            .Distinct()
            .ToArray();

        var ceremonies = requestIds.Length == 0
            ? new Dictionary<Guid, CeremonyAuthorizationCeremonyRef>()
            : await db.CeremonyRequests.AsNoTracking()
                .Where(x => requestIds.Contains(x.Id) && x.OrganizationId == organizationId)
                .Select(x => new CeremonyAuthorizationCeremonyRef(x.Id, x.CeremonyType, x.ProposedDate))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = documents
            .Where(x => x.RelatedCeremonyRequestId is not null && ceremonies.ContainsKey(x.RelatedCeremonyRequestId.Value))
            .Select(x =>
            {
                var ceremony = ceremonies[x.RelatedCeremonyRequestId!.Value];
                return new
                {
                    id = x.Id,
                    documentCode = x.DocumentCode,
                    title = x.Title,
                    ceremonyRequestId = ceremony.Id,
                    ceremonyType = ceremony.CeremonyType,
                    proposedDate = ceremony.ProposedDate,
                    issuedAtUtc = x.IssuedAtUtc
                };
            })
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> ListLodgeRecordsAsync(
        Guid organizationId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();
        var items = await db.LodgeSecretariatRecords.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.EventDate)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> UpsertLodgeRecordAsync(
        Guid organizationId,
        string recordType,
        Guid sourceRecordId,
        UpsertLodgeSecretariatRecordRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        DocumentManagementDbContext documentDb,
        GrandSecretariatDbContext grandSecretariatDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();
        if (!SecretariatOperationsCodes.RecordType.IsValid(recordType))
            return Results.BadRequest(new { message = "El tipo de registro no es válido." });

        DateOnly eventDate;
        string title;
        string? ceremonyType = null;

        if (recordType == SecretariatOperationsCodes.RecordType.LodgeMeeting)
        {
            var source = await lodgeDb.LodgeMeetings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == sourceRecordId && x.OrganizationId == organizationId, cancellationToken);
            if (source is null) return Results.NotFound(new { message = "La Tenida indicada no existe." });
            eventDate = source.MeetingDate;
            title = source.Title ?? $"Tenida {source.MeetingDate:dd-MM-yyyy}";
            ceremonyType = source.CeremonyType;
        }
        else if (recordType == SecretariatOperationsCodes.RecordType.AdministrativeMeeting)
        {
            var source = await db.LodgeAdministrativeMeetings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == sourceRecordId && x.OrganizationId == organizationId, cancellationToken);
            if (source is null) return Results.NotFound(new { message = "La reunión indicada no existe." });
            eventDate = source.MeetingDate;
            title = source.Title;
        }
        else
        {
            var source = await lodgeDb.LodgeCouncilSessions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == sourceRecordId && x.OrganizationId == organizationId, cancellationToken);
            if (source is null) return Results.NotFound(new { message = "El Consejo indicado no existe." });
            eventDate = source.SessionDate;
            title = $"Consejo de Administración {source.SessionDate:dd-MM-yyyy}";
        }

        if (request.WorkPaperDocumentVersionId is not null)
        {
            if (!SecretariatOperationsPolicy.WorkPaperAllowed(recordType, ceremonyType))
                return Results.BadRequest(new { message = "La plancha sólo puede asociarse a una Tenida no ceremonial." });
            if (request.WorkPaperAuthorMemberId is null)
                return Results.BadRequest(new { message = "Debe identificar al hermano autor de la plancha." });

            var authorBelongsToLodge = await db.Memberships.AsNoTracking()
                .AnyAsync(
                    x => x.MemberId == request.WorkPaperAuthorMemberId.Value &&
                         x.OrganizationId == organizationId &&
                         x.Status == MembershipCodes.MembershipStatus.Active &&
                         x.EndDate == null,
                    cancellationToken);
            if (!authorBelongsToLodge)
                return Results.BadRequest(new { message = "El autor de la plancha debe pertenecer activamente al Cuadro del Taller." });

            var valid = await ValidateDocumentVersionAsync(
                documentDb, request.WorkPaperDocumentVersionId.Value, organizationId, requirePdf: false, cancellationToken);
            if (!valid) return Results.BadRequest(new { message = "La plancha debe ser un documento disponible del Taller en PDF o Word." });
        }

        if (request.ExtractDocumentVersionId is not null)
        {
            var valid = await ValidateDocumentVersionAsync(
                documentDb, request.ExtractDocumentVersionId.Value, organizationId, requirePdf: true, cancellationToken);
            if (!valid) return Results.BadRequest(new { message = "El extracto debe ser un PDF disponible y perteneciente al Taller." });
        }

        if (request.FullMinuteDocumentVersionId is not null)
        {
            var valid = await ValidateDocumentVersionAsync(
                documentDb, request.FullMinuteDocumentVersionId.Value, organizationId, requirePdf: false, cancellationToken);
            if (!valid) return Results.BadRequest(new { message = "El acta completa debe ser un documento disponible del Taller en PDF o Word." });
        }

        if (request.CeremonyAuthorizationDocumentId is not null)
        {
            if (recordType != SecretariatOperationsCodes.RecordType.LodgeMeeting || ceremonyType is null)
                return Results.BadRequest(new { message = "La Plancha de Autorización sólo puede vincularse a una Tenida ceremonial." });

            var authorization = await grandSecretariatDb.SecretariatDocuments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.CeremonyAuthorizationDocumentId.Value, cancellationToken);
            var validAuthorization = authorization is not null &&
                authorization.OrganizationId == organizationId &&
                authorization.Status == GrandSecretariatCodes.DocumentStatus.Issued &&
                authorization.RelatedCeremonyRequestId is not null &&
                ((authorization.DocumentType == GrandSecretariatCodes.DocumentType.Plancha &&
                  authorization.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization) ||
                 authorization.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationLegacy ||
                 authorization.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationPlanchaLegacy);
            if (!validAuthorization)
                return Results.BadRequest(new { message = "La Plancha de Autorización indicada no es una autorización vigente de Gran Secretaría para este Taller." });

            var ceremony = await db.CeremonyRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == authorization!.RelatedCeremonyRequestId!.Value, cancellationToken);
            if (ceremony is null ||
                ceremony.OrganizationId != organizationId ||
                ceremony.CeremonyType != ceremonyType ||
                ceremony.ProposedDate != eventDate)
                return Results.BadRequest(new { message = "La Plancha de Autorización no corresponde al Taller, tipo o fecha de esta Tenida ceremonial." });
        }

        var record = await db.LodgeSecretariatRecords.SingleOrDefaultAsync(
            x => x.RecordType == recordType && x.SourceRecordId == sourceRecordId, cancellationToken);

        if (record is null)
        {
            record = new LodgeSecretariatRecord
            {
                OrganizationId = organizationId,
                RecordType = recordType,
                SourceRecordId = sourceRecordId,
                EventDate = eventDate,
                Title = title,
                Status = SecretariatOperationsCodes.SubmissionStatus.Draft,
                CreatedBySubject = GetSubject(httpContext.User)
            };
            db.LodgeSecretariatRecords.Add(record);
        }

        record.WorkPaperDocumentVersionId = request.WorkPaperDocumentVersionId;
        record.WorkPaperAuthorMemberId = request.WorkPaperDocumentVersionId is null ? null : request.WorkPaperAuthorMemberId;
        record.ExtractDocumentVersionId = request.ExtractDocumentVersionId;
        record.FullMinuteDocumentVersionId = request.FullMinuteDocumentVersionId;
        record.CeremonyAuthorizationDocumentId = request.CeremonyAuthorizationDocumentId;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(record);
    }

    private static async Task<IResult> SubmitTenidaExtractAsync(
        Guid organizationId,
        Guid meetingId,
        HttpContext httpContext,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeSecretariat(httpContext.User, organizationId)) return Results.Forbid();

        var meeting = await lodgeDb.LodgeMeetings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == meetingId && x.OrganizationId == organizationId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!LodgeManagementCodes.MeetingStatus.IsHeld(meeting.Status))
            return Results.Conflict(new { message = "Sólo una Tenida realizada puede remitir extracto a Gran Secretaría." });

        var record = await db.LodgeSecretariatRecords.SingleOrDefaultAsync(
            x => x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
                 x.SourceRecordId == meetingId &&
                 x.OrganizationId == organizationId,
            cancellationToken);
        if (record?.ExtractDocumentVersionId is null)
            return Results.Conflict(new { message = "Debe cargar el extracto PDF antes de remitir la Tenida." });

        var valid = await ValidateDocumentVersionAsync(
            documentDb, record.ExtractDocumentVersionId.Value, organizationId, requirePdf: true, cancellationToken);
        if (!valid) return Results.Conflict(new { message = "El extracto PDF aún no está disponible o no pertenece al Taller." });

        record.Status = SecretariatOperationsCodes.SubmissionStatus.Submitted;
        record.SubmittedBySubject = GetSubject(httpContext.User);
        record.SubmittedAtUtc = DateTimeOffset.UtcNow;
        record.ReviewNotes = null;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            record.Id,
            record.OrganizationId,
            record.RecordType,
            record.SourceRecordId,
            record.EventDate,
            record.Title,
            record.ExtractDocumentVersionId,
            record.Status,
            record.SubmittedAtUtc
        });
    }

    private static async Task<bool> ValidateDocumentVersionAsync(
        DocumentManagementDbContext db,
        Guid versionId,
        Guid organizationId,
        bool requirePdf,
        CancellationToken cancellationToken)
    {
        var item = await db.DocumentVersions.AsNoTracking()
            .Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
        if (item is null || item.Document.OrganizationId != organizationId ||
            item.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            return false;

        if (requirePdf) return item.ContentType == "application/pdf";
        return item.ContentType is "application/pdf" or
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    }

    private static async Task<Guid?> FindExistingMemberAsync(
        PmgmDbContext db,
        Guid organizationId,
        string? rut,
        string? institutionalNumber,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(rut))
        {
            var byRut = await db.Members.AsNoTracking()
                .Where(x => x.Person.Rut == rut)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (byRut is not null) return byRut;
        }

        if (!string.IsNullOrWhiteSpace(institutionalNumber))
        {
            return await db.Members.AsNoTracking()
                .Where(x => x.InstitutionalNumber == institutionalNumber)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private static string? ValidateIntakeRequest(HistoricalMemberIntakeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstNames) || string.IsNullOrWhiteSpace(request.LastNames))
            return "Nombres y apellidos son obligatorios.";
        if (!SecretariatOperationsCodes.CurrentDegree.IsValid(request.CurrentDegree))
            return "El grado actual no es válido.";
        if (!string.IsNullOrWhiteSpace(request.Rut) && !SecretariatOperationsPolicy.IsValidRut(request.Rut))
            return "El RUT no es válido.";
        if (!SecretariatOperationsPolicy.HasChronologicalMilestones(
                request.InitiationDate, request.WageIncreaseDate, request.ExaltationDate))
            return "Las fechas masónicas informadas no son cronológicamente consistentes.";
        if (string.IsNullOrWhiteSpace(request.EvidenceReference))
            return "Debe indicar la fuente o evidencia de la carga histórica.";
        if (request.Offices.Any(x => string.IsNullOrWhiteSpace(x.OfficeType) || string.IsNullOrWhiteSpace(x.Period)))
            return "Todo cargo debe indicar código institucional y período.";
        return null;
    }

    private static Dictionary<string, List<HistoricalOfficeRequest>> ParseOfficeRows(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, List<HistoricalOfficeRequest>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var key = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;
            var item = new HistoricalOfficeRequest(
                row.Cell(2).GetString().Trim(),
                row.Cell(3).GetString().Trim(),
                ParseDate(row.Cell(4)),
                ParseDate(row.Cell(5)),
                ParseBool(row.Cell(6)));
            if (!map.TryGetValue(key, out var items)) map[key] = items = [];
            items.Add(item);
        }
        return map;
    }

    private static HistoricalMemberIntakeRequest ParseIntakeRow(
        IXLRow row,
        IReadOnlyList<HistoricalOfficeRequest> offices)
        => new(
            row.Cell(2).GetString(),
            row.Cell(3).GetString(),
            NullIfWhiteSpace(row.Cell(4).GetString()),
            NullIfWhiteSpace(row.Cell(5).GetString()),
            NullIfWhiteSpace(row.Cell(6).GetString()),
            NullIfWhiteSpace(row.Cell(7).GetString()),
            row.Cell(8).GetString().Trim().ToLowerInvariant(),
            ParseDate(row.Cell(9)),
            ParseDate(row.Cell(10)),
            ParseDate(row.Cell(11)),
            ParseDate(row.Cell(12)),
            ParseDate(row.Cell(13)) ?? DateOnly.FromDateTime(DateTime.Today),
            row.Cell(14).GetString().Trim(),
            offices);

    private static DateOnly? ParseDate(IXLCell cell)
    {
        if (cell.TryGetValue<DateTime>(out var date)) return DateOnly.FromDateTime(date);
        var text = cell.GetString().Trim();
        return DateOnly.TryParse(text, out var parsed) ? parsed : null;
    }

    private static bool ParseBool(IXLCell cell)
    {
        if (cell.TryGetValue<bool>(out var value)) return value;
        return cell.GetString().Trim().ToLowerInvariant() is "si" or "sí" or "true" or "1" or "x";
    }

    private static object ToHistoricalIntakeDto(HistoricalMemberIntake x) => new
    {
        x.Id,
        x.OrganizationId,
        x.TargetMemberId,
        x.CutoffDate,
        x.FirstNames,
        x.LastNames,
        x.Rut,
        x.InstitutionalNumber,
        x.Email,
        x.Phone,
        x.CurrentDegree,
        x.MembershipStartDate,
        x.InitiationDate,
        x.WageIncreaseDate,
        x.ExaltationDate,
        x.EvidenceReference,
        x.Status,
        x.Revision,
        x.CreatedAtUtc,
        x.SubmittedAtUtc,
        x.ReviewedAtUtc,
        x.ReviewNotes,
        x.ApprovedMemberId,
        offices = x.Offices.Select(o => new { o.Id, o.OfficeType, o.Period, o.StartDate, o.EndDate, o.IsCurrent })
    };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub")
           ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? "unknown";
}

public sealed record HistoricalMemberIntakeRequest(
    string FirstNames,
    string LastNames,
    string? Rut,
    string? InstitutionalNumber,
    string? Email,
    string? Phone,
    string CurrentDegree,
    DateOnly? MembershipStartDate,
    DateOnly? InitiationDate,
    DateOnly? WageIncreaseDate,
    DateOnly? ExaltationDate,
    DateOnly CutoffDate,
    string EvidenceReference,
    IReadOnlyList<HistoricalOfficeRequest> Offices);

public sealed record HistoricalOfficeRequest(
    string OfficeType,
    string Period,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsCurrent);

public sealed record HistoricalMemberReviewRequest(string Decision, string? Notes);

public sealed record CreateAdministrativeMeetingRequest(DateOnly MeetingDate, string Title, string? Purpose);

public sealed record UpsertLodgeSecretariatRecordRequest(
    Guid? WorkPaperDocumentVersionId,
    Guid? WorkPaperAuthorMemberId,
    Guid? ExtractDocumentVersionId,
    Guid? FullMinuteDocumentVersionId,
    Guid? CeremonyAuthorizationDocumentId);

public sealed record CeremonyAuthorizationCeremonyRef(
    Guid Id,
    string CeremonyType,
    DateOnly? ProposedDate);
