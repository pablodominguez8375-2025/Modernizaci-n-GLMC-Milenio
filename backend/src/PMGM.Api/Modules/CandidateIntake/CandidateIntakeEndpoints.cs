using System.Security.Claims;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateIntakeEndpoints
{
    public static IEndpointRouteBuilder MapCandidateIntakeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/insinuados")
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();

        group.MapPut("/solicitudes/{requestId:guid}/ficha", UpsertProfileAsync);
        group.MapGet("/solicitudes/{requestId:guid}/ficha", GetProfileAsync);
        group.MapGet("/revision-gran-secretaria", GetGrandSecretariatQueueAsync);
        group.MapPost("/solicitudes/{requestId:guid}/foto", AttachPhotoAsync);
        group.MapPut("/solicitudes/{requestId:guid}/foto/contenido", UploadPhotoAsync);
        group.MapGet("/solicitudes/{requestId:guid}/foto", GetPrivatePhotoAsync);

        endpoints.MapGet("/api/candidate-publications/{publicationId:guid}/photo", GetPublishedPhotoAsync)
            .WithTags("Portal de Insinuados")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> UpsertProfileAsync(
        Guid requestId,
        CandidateIntakeUpsertRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await coreDb.CeremonyRequests
            .Include(x => x.CandidatePerson)
            .Include(x => x.Organization)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (ceremony is null) return Results.NotFound(new { message = "La solicitud de iniciación no existe." });
        if (ceremony.CeremonyType != CeremonyCodes.Type.Initiation || ceremony.CandidatePersonId is null || ceremony.CandidatePerson is null)
            return Results.BadRequest(new { message = "La ficha privada sólo aplica a solicitudes de iniciación." });
        if (!access.CanManageOrganization(httpContext.User, ceremony.OrganizationId)) return Results.Forbid();
        if (ceremony.Status is CeremonyCodes.RequestStatus.Authorized or CeremonyCodes.RequestStatus.Rejected)
            return Results.Conflict(new { message = "La solicitud se encuentra cerrada y no admite cambios en la ficha." });

        var validationError = ValidateRequest(request);
        if (validationError is not null) return Results.BadRequest(new { message = validationError });

        var subject = GetSubject(httpContext.User);
        var now = DateTimeOffset.UtcNow;
        var presenters = request.Presenters
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var profile = await intakeDb.CandidateIntakeProfiles
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, cancellationToken);

        var created = profile is null;
        if (profile is null)
        {
            profile = new CandidateIntakeProfile
            {
                CeremonyRequestId = ceremony.Id,
                OrganizationId = ceremony.OrganizationId,
                PersonId = ceremony.CandidatePersonId.Value,
                InsinuationDate = request.InsinuationDate,
                SubmittedBySubject = subject,
                SubmittedAtUtc = now,
                UpdatedBySubject = subject,
                UpdatedAtUtc = now
            };
            intakeDb.CandidateIntakeProfiles.Add(profile);
        }

        profile.PaternalSurname = Normalize(request.PaternalSurname);
        profile.MaternalSurname = Normalize(request.MaternalSurname);
        profile.RutOrInstitutionalId = Normalize(request.RutOrInstitutionalId);
        profile.BirthDate = request.BirthDate;
        profile.Nationality = Normalize(request.Nationality);
        profile.CivilStatus = Normalize(request.CivilStatus);
        profile.Occupation = Normalize(request.Occupation);
        profile.EmployerName = Normalize(request.EmployerName);
        profile.WorkAddress = Normalize(request.WorkAddress);
        profile.WorkPosition = Normalize(request.WorkPosition);
        profile.WorkPhone = Normalize(request.WorkPhone);
        profile.City = Normalize(request.City);
        profile.Orient = Normalize(request.Orient);
        profile.PresentersJson = JsonSerializer.Serialize(presenters);
        profile.InsinuationDate = request.InsinuationDate;
        profile.FirstDegreePresentationDate = request.FirstDegreePresentationDate;
        profile.ResponsibleSecretaryName = Normalize(request.ResponsibleSecretaryName);
        profile.InterviewSummary = Normalize(request.InterviewSummary);
        profile.InternalObservations = Normalize(request.InternalObservations);
        profile.UpdatedBySubject = subject;
        profile.UpdatedAtUtc = now;

        ceremony.CandidatePerson.FirstNames = request.FirstNames.Trim();
        ceremony.CandidatePerson.LastNames = string.Join(' ', new[] { request.PaternalSurname, request.MaternalSurname }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));
        ceremony.CandidatePerson.Email = Normalize(request.Email);
        ceremony.CandidatePerson.Phone = Normalize(request.Phone);
        ceremony.CandidatePerson.Address = Normalize(request.Address);
        ceremony.Status = CeremonyCodes.RequestStatus.UnderReview;

        await intakeDb.SaveChangesAsync(cancellationToken);

        coreDb.CeremonyValidations.Add(new PMGM.Api.Modules.Ceremonies.Entities.CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = CeremonyCodes.ValidationType.CandidatePublicationReview,
            Status = CeremonyCodes.ValidationStatus.Pending,
            AsOfDate = ChileToday(),
            SourceReference = profile.Id.ToString(),
            Notes = created
                ? "Ficha de insinuado ingresada por Secretaría de Taller y enviada a revisión de Gran Secretaría."
                : "Ficha de insinuado actualizada y reenviada a revisión de Gran Secretaría."
        });

        audit.Add(
            httpContext,
            created ? "candidate.intake.created" : "candidate.intake.updated",
            nameof(CandidateIntakeProfile),
            profile.Id.ToString(),
            ceremony.OrganizationId,
            AuditResults.Success,
            new
            {
                profile.CeremonyRequestId,
                profile.PersonId,
                profile.InsinuationDate,
                presenterCount = presenters.Length,
                hasPhoto = profile.PhotoVersionId is not null,
                reviewStatus = CandidateIntakeCodes.ReviewStatus.Pending
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";

        return Results.Ok(await BuildProfileDtoAsync(profile, ceremony.CandidatePerson, ceremony.Organization.Name, ceremony.Organization.Number, coreDb, cancellationToken));
    }

    private static async Task<IResult> GetProfileAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var profile = await intakeDb.CandidateIntakeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, cancellationToken);
        if (profile is null) return Results.NotFound();

        if (!access.CanManageOrganization(httpContext.User, profile.OrganizationId) &&
            !access.CanManageGrandSecretariat(httpContext.User))
            return Results.Forbid();

        var ceremony = await coreDb.CeremonyRequests.AsNoTracking()
            .Include(x => x.CandidatePerson)
            .Include(x => x.Organization)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony?.CandidatePerson is null) return Results.NotFound();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await BuildProfileDtoAsync(profile, ceremony.CandidatePerson, ceremony.Organization.Name, ceremony.Organization.Number, coreDb, cancellationToken));
    }

    private static async Task<IResult> GetGrandSecretariatQueueAsync(
        string? status,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();
        if (!string.IsNullOrWhiteSpace(status) && !CandidateIntakeCodes.ReviewStatus.IsValid(status))
            return Results.BadRequest(new { message = "El estado de revisión indicado no es válido." });

        var profiles = await intakeDb.CandidateIntakeProfiles.AsNoTracking()
            .OrderByDescending(x => x.SubmittedAtUtc)
            .Take(300)
            .ToListAsync(cancellationToken);

        var requestIds = profiles.Select(x => x.CeremonyRequestId).ToArray();
        var ceremonies = await coreDb.CeremonyRequests.AsNoTracking()
            .Where(x => requestIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Organization.Name,
                x.Organization.Number,
                FirstNames = x.CandidatePerson != null ? x.CandidatePerson.FirstNames : "",
                LastNames = x.CandidatePerson != null ? x.CandidatePerson.LastNames : ""
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var reviews = await coreDb.CeremonyValidations.AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId) && x.ValidationType == CeremonyCodes.ValidationType.CandidatePublicationReview)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.CeremonyRequestId, x.Status, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);
        var latestReview = reviews.GroupBy(x => x.CeremonyRequestId).ToDictionary(x => x.Key, x => x.First());

        var publishedRequestIds = await coreDb.CandidatePublications.AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId) && x.Status == CeremonyCodes.PublicationStatus.Published)
            .Select(x => x.CeremonyRequestId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var published = publishedRequestIds.ToHashSet();

        var rows = new List<CandidateReviewQueueItemDto>();
        foreach (var profile in profiles)
        {
            if (!ceremonies.TryGetValue(profile.CeremonyRequestId, out var ceremony)) continue;
            var reviewStatus = published.Contains(profile.CeremonyRequestId)
                ? CandidateIntakeCodes.ReviewStatus.Approved
                : latestReview.TryGetValue(profile.CeremonyRequestId, out var review)
                    ? MapReviewStatus(review.Status)
                    : CandidateIntakeCodes.ReviewStatus.Pending;

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, reviewStatus, StringComparison.OrdinalIgnoreCase)) continue;

            rows.Add(new CandidateReviewQueueItemDto(
                profile.CeremonyRequestId,
                $"{ceremony.FirstNames} {ceremony.LastNames}".Trim(),
                ceremony.Name,
                ceremony.Number,
                profile.InsinuationDate,
                profile.SubmittedAtUtc,
                profile.PhotoVersionId is not null,
                reviewStatus));
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = rows.Count, items = rows });
    }

    private static async Task<IResult> AttachPhotoAsync(
        Guid requestId,
        CandidatePhotoLinkRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var profile = await intakeDb.CandidateIntakeProfiles.SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, cancellationToken);
        if (profile is null) return Results.NotFound(new { message = "Primero debe registrar la ficha del insinuado." });
        if (!access.CanManageOrganization(httpContext.User, profile.OrganizationId)) return Results.Forbid();

        var version = await documentDb.DocumentVersions.AsNoTracking()
            .Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == request.PhotoVersionId, cancellationToken);
        if (version is null) return Results.NotFound(new { message = "La versión documental indicada no existe." });
        if (version.Document.OrganizationId != profile.OrganizationId)
            return Results.BadRequest(new { message = "La fotografía debe pertenecer al expediente documental del mismo Taller." });
        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            return Results.Conflict(new { message = "La fotografía debe completar carga y análisis antivirus antes de ser vinculada." });
        if (!CandidateIntakeCodes.PhotoContentType.IsAllowed(version.ContentType))
            return Results.BadRequest(new { message = "La foto tipo pasaporte debe ser JPEG o PNG." });
        if (version.SizeBytes > 100 * 1024)
            return Results.BadRequest(new { message = "La foto tipo pasaporte no puede superar 100 KB." });

        profile.PhotoVersionId = version.Id;
        profile.UpdatedBySubject = GetSubject(httpContext.User);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await intakeDb.SaveChangesAsync(cancellationToken);

        coreDb.CeremonyValidations.Add(new PMGM.Api.Modules.Ceremonies.Entities.CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = CeremonyCodes.ValidationType.CandidatePublicationReview,
            Status = CeremonyCodes.ValidationStatus.Pending,
            AsOfDate = ChileToday(),
            SourceReference = profile.Id.ToString(),
            Notes = "Fotografía tipo pasaporte vinculada y ficha reenviada a revisión de Gran Secretaría."
        });
        audit.Add(httpContext, "candidate.intake.photo_linked", nameof(CandidateIntakeProfile), profile.Id.ToString(), profile.OrganizationId,
            AuditResults.Success, new { profile.CeremonyRequestId, profile.PhotoVersionId });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { profile.CeremonyRequestId, photoAvailable = true });
    }

    private static async Task<IResult> UploadPhotoAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        IDocumentMalwareScanner scanner,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var profile = await intakeDb.CandidateIntakeProfiles.SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, cancellationToken);
        if (profile is null) return Results.NotFound(new { message = "Primero debe guardar la ficha del insinuado." });
        if (!access.CanManageOrganization(httpContext.User, profile.OrganizationId)) return Results.Forbid();

        var contentType = httpContext.Request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/png")) return Results.BadRequest(new { message = "La fotografía debe ser JPG o PNG." });
        var length = httpContext.Request.ContentLength;
        if (length is null or <= 0 || length > CandidatePhotoPolicy.MaxBytes)
            return Results.BadRequest(new { message = "La fotografía debe pesar como máximo 100 KB." });

        await using var buffer = new MemoryStream((int)length.Value);
        await httpContext.Request.Body.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != length.Value || buffer.Length > CandidatePhotoPolicy.MaxBytes)
            return Results.BadRequest(new { message = "El tamaño recibido de la fotografía no es válido." });
        var bytes = buffer.ToArray();
        if (!CandidatePhotoPolicy.MeetsMinimumDimensions(bytes, contentType, out var width, out var height))
            return Results.BadRequest(new { message = "La fotografía debe ser válida y tener al menos 500 × 500 píxeles." });

        DocumentMalwareScanResult scan;
        try { scan = await scanner.ScanAsync(new MemoryStream(bytes, writable: false), cancellationToken); }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException)
        { return Results.Json(new { message = "El análisis antivirus no está disponible; intente nuevamente." }, statusCode: StatusCodes.Status503ServiceUnavailable); }
        if (!scan.IsClean) return Results.UnprocessableEntity(new { message = "La fotografía fue rechazada por el análisis antivirus." });

        var subject = GetSubject(httpContext.User);
        var collectionCode = $"CANDIDATE-PHOTOS-{profile.OrganizationId:N}";
        var collection = await documentDb.DocumentCollections.SingleOrDefaultAsync(x => x.Code == collectionCode, cancellationToken);
        if (collection is null)
        {
            collection = new DocumentCollection { Code = collectionCode, Name = "Fotografías privadas de insinuados", Description = "Evidencia fotográfica privada del circuito de iniciación.", Scope = DocumentManagementCodes.Scope.Organization, OrganizationId = profile.OrganizationId, Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = subject };
            documentDb.DocumentCollections.Add(collection);
        }
        var document = new InstitutionalDocument { Collection = collection, OrganizationId = profile.OrganizationId, Title = $"Fotografía de insinuado {requestId:N}", DocumentType = "candidate_passport_photo", Classification = DocumentManagementCodes.Classification.Sensitive, AccessPolicy = DocumentManagementCodes.AccessPolicy.ManagementOnly, Status = DocumentManagementCodes.DocumentStatus.Active, CreatedBySubject = subject };
        var encodedFileName = httpContext.Request.Headers["X-File-Name"].ToString();
        string fileName;
        try { fileName = Path.GetFileName(Uri.UnescapeDataString(encodedFileName)); }
        catch (UriFormatException) { return Results.BadRequest(new { message = "El nombre codificado del archivo no es válido." }); }
        if (string.IsNullOrWhiteSpace(fileName)) fileName = contentType == "image/png" ? "foto-insinuado.png" : "foto-insinuado.jpg";
        if (fileName.Length > 500) return Results.BadRequest(new { message = "El nombre del archivo no puede superar 500 caracteres." });
        var version = new DocumentVersion { Document = document, VersionNumber = 1, OriginalFileName = fileName, ContentType = contentType, SizeBytes = bytes.LongLength, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available, ScanReference = scan.EvidenceReference, CreatedBySubject = subject };
        version.ObjectKey = DocumentObjectKeyFactory.Create(document.Id, version.Id);
        await objectStore.StoreAsync(version.ObjectKey, new MemoryStream(bytes, writable: false), contentType, cancellationToken);
        documentDb.InstitutionalDocuments.Add(document);
        documentDb.DocumentVersions.Add(version);
        await documentDb.SaveChangesAsync(cancellationToken);

        profile.PhotoVersionId = version.Id;
        profile.UpdatedBySubject = subject;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await intakeDb.SaveChangesAsync(cancellationToken);
        coreDb.CeremonyValidations.Add(new PMGM.Api.Modules.Ceremonies.Entities.CeremonyValidation { CeremonyRequestId = requestId, ValidationType = CeremonyCodes.ValidationType.CandidatePublicationReview, Status = CeremonyCodes.ValidationStatus.Pending, AsOfDate = ChileToday(), SourceReference = profile.Id.ToString(), Notes = "Fotografía cargada, validada y ficha reenviada a revisión de Gran Secretaría." });
        audit.Add(httpContext, "candidate.intake.photo_uploaded", nameof(CandidateIntakeProfile), profile.Id.ToString(), profile.OrganizationId, AuditResults.Success, new { profile.CeremonyRequestId, profile.PhotoVersionId, width, height, bytes = version.SizeBytes });
        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { profile.CeremonyRequestId, photoAvailable = true, width, height, sizeBytes = version.SizeBytes });
    }

    private static async Task<IResult> GetPrivatePhotoAsync(
        Guid requestId,
        HttpContext httpContext,
        CandidateIntakeDbContext intakeDb,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var profile = await intakeDb.CandidateIntakeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, cancellationToken);
        if (profile?.PhotoVersionId is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, profile.OrganizationId) &&
            !access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();

        return await StreamPhotoAsync(profile.PhotoVersionId.Value, httpContext, documentDb, objectStore, cancellationToken);
    }

    private static async Task<IResult> GetPublishedPhotoAsync(
        Guid publicationId,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        DocumentManagementDbContext documentDb,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var publication = await coreDb.CandidatePublications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == publicationId &&
                                       x.Status == CeremonyCodes.PublicationStatus.Published &&
                                       x.PublishedFromUtc <= now &&
                                       (x.PublishedUntilUtc == null || x.PublishedUntilUtc >= now), cancellationToken);
        if (publication is null) return Results.NotFound();

        var profile = await intakeDb.CandidateIntakeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == publication.CeremonyRequestId, cancellationToken);
        if (profile?.PhotoVersionId is null) return Results.NotFound();

        return await StreamPhotoAsync(profile.PhotoVersionId.Value, httpContext, documentDb, objectStore, cancellationToken);
    }

    private static async Task<IResult> StreamPhotoAsync(
        Guid photoVersionId,
        HttpContext httpContext,
        DocumentManagementDbContext documentDb,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var version = await documentDb.DocumentVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == photoVersionId, cancellationToken);
        if (version is null || version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available ||
            !CandidateIntakeCodes.PhotoContentType.IsAllowed(version.ContentType)) return Results.NotFound();
        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken)) return Results.NotFound();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        var stream = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken);
        return Results.Stream(stream, version.ContentType, enableRangeProcessing: false);
    }

    private static async Task<CandidateIntakeProfileDto> BuildProfileDtoAsync(
        CandidateIntakeProfile profile,
        PMGM.Api.Modules.Core.Entities.Person person,
        string workshopName,
        string? workshopNumber,
        PmgmDbContext coreDb,
        CancellationToken cancellationToken)
    {
        var latestReview = await coreDb.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == profile.CeremonyRequestId && x.ValidationType == CeremonyCodes.ValidationType.CandidatePublicationReview)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(cancellationToken);
        var published = await coreDb.CandidatePublications.AsNoTracking()
            .AnyAsync(x => x.CeremonyRequestId == profile.CeremonyRequestId && x.Status == CeremonyCodes.PublicationStatus.Published, cancellationToken);

        return new CandidateIntakeProfileDto(
            profile.CeremonyRequestId,
            person.FirstNames,
            profile.PaternalSurname,
            profile.MaternalSurname,
            profile.RutOrInstitutionalId,
            profile.BirthDate,
            profile.Nationality,
            profile.CivilStatus,
            profile.Occupation,
            profile.EmployerName,
            profile.WorkAddress,
            profile.WorkPosition,
            profile.WorkPhone,
            person.Phone,
            person.Email,
            person.Address,
            profile.City,
            workshopName,
            workshopNumber,
            profile.Orient,
            JsonSerializer.Deserialize<string[]>(profile.PresentersJson) ?? [],
            profile.InsinuationDate,
            profile.FirstDegreePresentationDate,
            profile.ResponsibleSecretaryName,
            published ? CandidateIntakeCodes.ReviewStatus.Approved : MapReviewStatus(latestReview),
            profile.PhotoVersionId is not null,
            profile.InterviewSummary,
            profile.InternalObservations,
            profile.SubmittedAtUtc,
            profile.UpdatedAtUtc);
    }

    private static string MapReviewStatus(string? status)
        => status switch
        {
            CeremonyCodes.ValidationStatus.Approved => CandidateIntakeCodes.ReviewStatus.Approved,
            CeremonyCodes.ValidationStatus.Observed => CandidateIntakeCodes.ReviewStatus.Observed,
            CeremonyCodes.ValidationStatus.Rejected => CandidateIntakeCodes.ReviewStatus.Rejected,
            _ => CandidateIntakeCodes.ReviewStatus.Pending
        };

    private static string? ValidateRequest(CandidateIntakeUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstNames) || request.FirstNames.Length > 160) return "Los nombres son obligatorios y no pueden exceder 160 caracteres.";
        if (string.IsNullOrWhiteSpace(request.PaternalSurname) || request.PaternalSurname.Length > 160) return "El apellido paterno es obligatorio y no puede exceder 160 caracteres.";
        if (request.MaternalSurname?.Length > 160) return "El apellido materno no puede exceder 160 caracteres.";
        if (request.RutOrInstitutionalId?.Length > 80) return "El RUT/ID no puede exceder 80 caracteres.";
        if (request.Email?.Length > 320 || request.Phone?.Length > 80 || request.Address?.Length > 500) return "Uno de los datos de contacto excede el máximo permitido.";
        if (request.EmployerName?.Length > 240 || request.WorkAddress?.Length > 500 || request.WorkPosition?.Length > 240 || request.WorkPhone?.Length > 80) return "Uno de los antecedentes laborales excede el máximo permitido.";
        if (request.Presenters.Count is < 1 or > 8 || request.Presenters.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200)) return "Debe registrar entre 1 y 8 presentantes válidos.";
        if (request.InsinuationDate > ChileToday()) return "La fecha de insinuación no puede estar en el futuro.";
        if (request.FirstDegreePresentationDate is not null && request.FirstDegreePresentationDate > ChileToday()) return "La presentación en 1.er grado no puede estar en el futuro.";
        if (request.FirstDegreePresentationDate is not null && request.FirstDegreePresentationDate < request.InsinuationDate) return "La presentación en 1.er grado no puede ser anterior al ingreso de la insinuación.";
        if (request.ResponsibleSecretaryName?.Length > 240) return "El nombre del secretario responsable no puede exceder 240 caracteres.";
        if (request.BirthDate is not null && request.BirthDate > ChileToday()) return "La fecha de nacimiento no puede estar en el futuro.";
        if (request.InterviewSummary?.Length > 4000 || request.InternalObservations?.Length > 4000) return "Las observaciones no pueden exceder 4000 caracteres.";
        return null;
    }

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly ChileToday()
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record CandidateIntakeUpsertRequest(
    string FirstNames,
    string PaternalSurname,
    string? MaternalSurname,
    string? RutOrInstitutionalId,
    DateOnly? BirthDate,
    string? Nationality,
    string? CivilStatus,
    string? Occupation,
    string? EmployerName,
    string? WorkAddress,
    string? WorkPosition,
    string? WorkPhone,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string? Orient,
    IReadOnlyList<string> Presenters,
    DateOnly InsinuationDate,
    DateOnly? FirstDegreePresentationDate,
    string? ResponsibleSecretaryName,
    string? InterviewSummary,
    string? InternalObservations);

public sealed record CandidatePhotoLinkRequest(Guid PhotoVersionId);

public sealed record CandidateIntakeProfileDto(
    Guid CeremonyRequestId,
    string FirstNames,
    string? PaternalSurname,
    string? MaternalSurname,
    string? RutOrInstitutionalId,
    DateOnly? BirthDate,
    string? Nationality,
    string? CivilStatus,
    string? Occupation,
    string? EmployerName,
    string? WorkAddress,
    string? WorkPosition,
    string? WorkPhone,
    string? Phone,
    string? Email,
    string? Address,
    string? City,
    string WorkshopName,
    string? WorkshopNumber,
    string? Orient,
    IReadOnlyList<string> Presenters,
    DateOnly InsinuationDate,
    DateOnly? FirstDegreePresentationDate,
    string? ResponsibleSecretaryName,
    string ReviewStatus,
    bool PhotoAvailable,
    string? InterviewSummary,
    string? InternalObservations,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CandidateReviewQueueItemDto(
    Guid CeremonyRequestId,
    string DisplayName,
    string WorkshopName,
    string? WorkshopNumber,
    DateOnly InsinuationDate,
    DateTimeOffset SubmittedAtUtc,
    bool PhotoAvailable,
    string ReviewStatus);
