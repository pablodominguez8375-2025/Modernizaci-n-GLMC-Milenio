using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Private extra fields for an already-created candidate dossier.
/// Never replaces institutional identity, candidacy decisions or existing
/// CandidateIntakeUpsertRequest; historical submissions are append-only.
/// </summary>
public static class CandidateFormCaptureEndpoints
{
    private const string FormCode = FormSchemaInstitutionalCatalog.CandidateProfile;

    public static IEndpointRouteBuilder MapCandidateFormCaptureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/insinuados/solicitudes/{requestId:guid}/formularios/capturas")
            .WithTags("Insinuados — capturas privadas versionadas")
            .RequireAuthorization();

        group.MapGet("/schema", GetActiveSchemaAsync);
        group.MapGet("/", GetCapturesAsync);
        group.MapGet("/{captureRevision:int}", GetCaptureAsync);
        group.MapGet("/{captureRevision:int}/export.csv", ExportCaptureCsvAsync);
        group.MapPost("/", CreateCaptureAsync);
        return endpoints;
    }

    /// <summary>
    /// Authorized metadata for a future responsive renderer. Returns ONLY
    /// configurable fields; canonical candidate identity is read from the
    /// existing protected candidate profile endpoints.
    /// </summary>
    private static async Task<IResult> GetActiveSchemaAsync(
        Guid requestId, HttpContext http, CandidateIntakeDbContext intakeDb,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        var profile = await LoadAuthorizedProfileAsync(requestId, http, intakeDb, access,
            allowGrandSecretariat: true, ct);
        if (profile.Status == ProfileAccessStatus.Forbidden) return Results.Forbid();
        if (profile.Profile is null) return Results.NotFound();

        NoCache(http);
        var lodgeScope = FormSchemaScope.ForOrganization(profile.Profile.OrganizationId);
        var now = DateTimeOffset.UtcNow;
        var active = await db.FormSchemaVersions.AsNoTracking()
            .Where(x => x.FormCode == FormCode &&
                x.ScopeKey == lodgeScope && x.EffectiveFromUtc <= now)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        active ??= await db.FormSchemaVersions.AsNoTracking()
            .Where(x => x.FormCode == FormCode &&
                x.ScopeKey == FormSchemaScope.Order && x.EffectiveFromUtc <= now)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (active is null) return Results.NotFound();

        var trusted = FormSchemaVersionStore.Decode(active);
        if (!FormSchemaInstitutionalCatalog.ValidateForPublication(trusted).IsValid)
            throw new InvalidDataException("La definición vigente del formulario no cumple el catálogo institucional.");

        var previous = await db.CandidateFormCaptures.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId)
            .OrderByDescending(x => x.CaptureRevision)
            .Select(x => x.CaptureRevision)
            .FirstOrDefaultAsync(ct);
        var visible = trusted.Fields.Where(x => !x.IsCore && x.Enabled && x.Visible).ToArray();
        return Results.Ok(new
        {
            formCode = trusted.FormCode,
            schemaVersion = trusted.Version,
            scopeKey = active.ScopeKey,
            trusted.EffectiveFromUtc,
            expectedCaptureRevision = previous,
            fields = visible
        });
    }

    private static async Task<IResult> GetCapturesAsync(
        Guid requestId, HttpContext http, CandidateIntakeDbContext intakeDb,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        var profile = await LoadAuthorizedProfileAsync(requestId, http, intakeDb, access,
            allowGrandSecretariat: true, ct);
        if (profile.Status == ProfileAccessStatus.Forbidden) return Results.Forbid();
        if (profile.Profile is null) return Results.NotFound();

        NoCache(http);
        var captures = await db.CandidateFormCaptures.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.OrganizationId == profile.Profile.OrganizationId)
            .OrderByDescending(x => x.CaptureRevision)
            .Select(x => new
            {
                x.CaptureRevision, x.SchemaVersion, x.ScopeKey,
                x.PayloadSha256, x.CapturedAtUtc
            })
            .ToListAsync(ct);
        return Results.Ok(new { requestId, total = captures.Count, captures });
    }

    private static async Task<IResult> GetCaptureAsync(
        Guid requestId, int captureRevision, HttpContext http,
        CandidateIntakeDbContext intakeDb, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var profile = await LoadAuthorizedProfileAsync(requestId, http, intakeDb, access,
            allowGrandSecretariat: true, ct);
        if (profile.Status == ProfileAccessStatus.Forbidden) return Results.Forbid();
        if (profile.Profile is null || captureRevision < 1) return Results.NotFound();

        NoCache(http);
        var row = await db.CandidateFormCaptures.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId &&
                x.OrganizationId == profile.Profile.OrganizationId &&
                x.CaptureRevision == captureRevision, ct);
        if (row is null) return Results.NotFound();

        if (!string.Equals(row.PayloadSha256,
                FormSchemaVersionStore.Fingerprint(row.ValuesJson), StringComparison.Ordinal))
            throw new InvalidDataException("La captura histórica no supera el control de integridad.");

        var schema = await db.FormSchemaVersions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == row.SchemaVersionId && x.Version == row.SchemaVersion &&
                 x.ScopeKey == row.ScopeKey && x.FormCode == FormCode, ct);
        if (schema is null)
            throw new InvalidDataException("La captura no conserva su referencia de esquema.");
        var historical = FormSchemaVersionStore.Decode(schema);

        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ValuesJson)
            ?? throw new InvalidDataException("Los datos históricos no tienen una estructura válida.");

        audit.Add(http, "candidate.form_capture.read", nameof(CandidateFormCapture),
            row.Id.ToString("D"), row.OrganizationId, AuditResults.Success,
            new { row.CeremonyRequestId, row.CaptureRevision, row.SchemaVersion });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            row.CeremonyRequestId, row.CaptureRevision, row.SchemaVersion,
            row.ScopeKey, row.CapturedAtUtc, values,
            fields = historical.Fields.Where(x => !x.IsCore).ToArray()
        });
    }

    /// <summary>
    /// Authorized, audited one-revision CSV export. The document is generated
    /// only on request, never cached or published in the static showcase.
    /// </summary>
    private static async Task<IResult> ExportCaptureCsvAsync(
        Guid requestId, int captureRevision, HttpContext http,
        CandidateIntakeDbContext intakeDb, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var profile = await LoadAuthorizedProfileAsync(requestId, http, intakeDb, access,
            allowGrandSecretariat: true, ct);
        if (profile.Status == ProfileAccessStatus.Forbidden) return Results.Forbid();
        if (profile.Profile is null || captureRevision < 1) return Results.NotFound();

        NoCache(http);
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";

        var row = await db.CandidateFormCaptures.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId &&
                x.OrganizationId == profile.Profile.OrganizationId &&
                x.CaptureRevision == captureRevision, ct);
        if (row is null) return Results.NotFound();

        if (!string.Equals(row.PayloadSha256,
                FormSchemaVersionStore.Fingerprint(row.ValuesJson), StringComparison.Ordinal))
            throw new InvalidDataException("La captura histórica no supera el control de integridad.");

        var schema = await db.FormSchemaVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == row.SchemaVersionId &&
                x.Version == row.SchemaVersion &&
                x.ScopeKey == row.ScopeKey &&
                x.FormCode == FormCode, ct);
        if (schema is null)
            throw new InvalidDataException("La captura histórica no conserva la versión de su esquema.");

        var historical = FormSchemaVersionStore.Decode(schema);
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ValuesJson)
            ?? throw new InvalidDataException("La captura histórica no tiene valores exportables.");
        var content = CandidateFormCaptureCsv.Render(row, historical, values);

        audit.Add(http, "candidate.form_capture.export_csv", nameof(CandidateFormCapture),
            row.Id.ToString("D"), row.OrganizationId, AuditResults.Success,
            new
            {
                row.CeremonyRequestId, row.CaptureRevision,
                row.SchemaVersion, format = "csv", fieldCount = values.Count
            });
        await db.SaveChangesAsync(ct);

        var filename = $"insinuado-campos-adicionales-{requestId:N}-r{captureRevision}.csv";
        return Results.File(content, contentType: "text/csv; charset=utf-8", fileDownloadName: filename);
    }

    private static async Task<IResult> CreateCaptureAsync(
        Guid requestId, CandidateFormCaptureRequest request, HttpContext http,
        CandidateIntakeDbContext intakeDb, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var profile = await LoadAuthorizedProfileAsync(requestId, http, intakeDb, access,
            allowGrandSecretariat: false, ct);
        if (profile.Status == ProfileAccessStatus.Forbidden) return Results.Forbid();
        if (profile.Profile is null) return Results.NotFound();

        NoCache(http);
        if (request.SchemaVersion < 1 || request.ExpectedCaptureRevision < 0 || request.Values is null)
            return Results.BadRequest(new { message = "Versión, revisión y campos son obligatorios." });

        var subject = http.User.FindFirstValue("sub") ??
            http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 320)
            return Results.Forbid();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Synchronize with schema publications in both possible trusted scopes
        // before selecting the active version, then serialize submissions per
        // candidate request. No client-provided scope or form definition is used.
        var lodgeScope = FormSchemaScope.ForOrganization(profile.Profile.OrganizationId);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({FormSchemaScope.Order}), hashtext({FormCode}))", ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lodgeScope}), hashtext({FormCode}))", ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({"candidate-form-capture"}), hashtext({requestId.ToString("N")}))", ct);

        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null || ceremony.OrganizationId != profile.Profile.OrganizationId ||
            ceremony.CandidatePersonId != profile.Profile.PersonId ||
            ceremony.CeremonyType != CeremonyCodes.Type.Initiation)
            return Results.Conflict(new { message = "El expediente no coincide con su Taller e identidad." });
        if (ceremony.Status is CeremonyCodes.RequestStatus.Authorized or CeremonyCodes.RequestStatus.Rejected ||
            await db.CandidatePublications.AsNoTracking().AnyAsync(x =>
                x.CeremonyRequestId == requestId &&
                x.Status == CeremonyCodes.PublicationStatus.Published, ct))
            return Results.Conflict(new { message = "El expediente está cerrado o publicado." });

        var now = DateTimeOffset.UtcNow;
        var active = await db.FormSchemaVersions.AsNoTracking()
            .Where(x => x.FormCode == FormCode &&
                x.ScopeKey == lodgeScope && x.EffectiveFromUtc <= now)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        active ??= await db.FormSchemaVersions.AsNoTracking()
            .Where(x => x.FormCode == FormCode &&
                x.ScopeKey == FormSchemaScope.Order && x.EffectiveFromUtc <= now)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        if (active is null)
            return Results.Conflict(new { message = "No existe una definición publicada y vigente para este expediente." });
        if (request.SchemaVersion != active.Version)
            return Results.Conflict(new { message = "La versión enviada ya no es la vigente; actualice el formulario." });

        var trusted = FormSchemaVersionStore.Decode(active);
        var validation = CandidateFormCapturePolicy.Validate(trusted, request.SchemaVersion, request.Values);
        if (!validation.IsValid)
            return Results.BadRequest(new { errors = validation.Errors });

        var lastRevision = await db.CandidateFormCaptures.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId)
            .OrderByDescending(x => x.CaptureRevision)
            .Select(x => x.CaptureRevision)
            .FirstOrDefaultAsync(ct);
        if (lastRevision != request.ExpectedCaptureRevision)
            return Results.Conflict(new { message = "La captura fue actualizada por otra sesión. Recargue su historial." });

        var payload = JsonSerializer.Serialize(request.Values);
        var capture = new CandidateFormCapture
        {
            CeremonyRequestId = requestId,
            OrganizationId = profile.Profile.OrganizationId,
            SchemaVersionId = active.Id,
            SchemaVersion = active.Version,
            ScopeKey = active.ScopeKey,
            CaptureRevision = lastRevision + 1,
            ValuesJson = payload,
            PayloadSha256 = FormSchemaVersionStore.Fingerprint(payload),
            CapturedBySubject = subject
        };
        db.CandidateFormCaptures.Add(capture);
        audit.Add(http, "candidate.form_capture.created", nameof(CandidateFormCapture),
            capture.Id.ToString("D"), capture.OrganizationId, AuditResults.Success,
            new
            {
                capture.CeremonyRequestId, capture.CaptureRevision,
                capture.SchemaVersion, capture.ScopeKey,
                fields = request.Values.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray()
            });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var location = $"/api/insinuados/solicitudes/{requestId:D}/formularios/capturas/{capture.CaptureRevision}";
        return Results.Created(location, new
        {
            capture.CeremonyRequestId, capture.CaptureRevision,
            capture.SchemaVersion, capture.CapturedAtUtc,
            capture.PayloadSha256
        });
    }

    private static async Task<ProfileAccess> LoadAuthorizedProfileAsync(
        Guid requestId, HttpContext http, CandidateIntakeDbContext intakeDb,
        IInstitutionalAccessService access, bool allowGrandSecretariat, CancellationToken ct)
    {
        var profile = await intakeDb.CandidateIntakeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, ct);
        if (profile is null) return new(null, ProfileAccessStatus.Missing);
        if (!access.CanManageOrganization(http.User, profile.OrganizationId) &&
            !(allowGrandSecretariat && access.CanManageGrandSecretariat(http.User)))
            return new(null, ProfileAccessStatus.Forbidden);
        return new(profile, ProfileAccessStatus.Authorized);
    }

    private static void NoCache(HttpContext http)
        => http.Response.Headers.CacheControl = "private, no-store";

    private enum ProfileAccessStatus { Missing, Authorized, Forbidden }
    private sealed record ProfileAccess(CandidateIntakeProfile? Profile, ProfileAccessStatus Status);
}

public sealed record CandidateFormCaptureRequest(
    int SchemaVersion,
    int ExpectedCaptureRevision,
    IReadOnlyDictionary<string, JsonElement>? Values);
