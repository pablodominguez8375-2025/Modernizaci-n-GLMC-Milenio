using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Audit.Entities;

namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Internal persistence boundary for published form definitions. It is deliberately
/// NOT exposed as HTTP: a later administrative API must authorize the actor, resolve
/// trusted core keys and institutional scope before calling PublishAsync or ReadAsync.
/// </summary>
public sealed class FormSchemaVersionStore(PmgmDbContext db)
{
    public async Task<FormSchemaVersion> PublishAsync(
        FormSchemaSnapshot next,
        Guid? organizationId,
        IReadOnlyCollection<string> trustedCoreKeys,
        string actorSubject,
        string correlationId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(trustedCoreKeys);
        if (string.IsNullOrWhiteSpace(actorSubject) || actorSubject.Length > 320)
            throw new ArgumentException("Se requiere un sujeto institucional autenticado.", nameof(actorSubject));
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            throw new ArgumentException("Se requiere un identificador de correlación válido.", nameof(correlationId));

        // PostgreSQL timestamptz persists microseconds while .NET permits
        // 100-ns ticks. Canonicalize BEFORE validation and serialization so
        // historical metadata and SHA-256 agree after an EF readback.
        var effectiveUtc = next.EffectiveFromUtc.ToUniversalTime();
        var persistedTicks = effectiveUtc.Ticks - effectiveUtc.Ticks % 10;
        next = next with
        {
            EffectiveFromUtc = new DateTimeOffset(persistedTicks, TimeSpan.Zero)
        };

        var scope = FormSchemaScope.ForOrganization(organizationId);
        if (string.IsNullOrWhiteSpace(next.FormCode) || next.FormCode.Length > 64)
            throw new ArgumentException("Código de formulario inválido.", nameof(next));

        // Advisory lock serializes publishers for the same scope/form, including
        // publication of version one. The unique database index is a second guard.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({scope}), hashtext({next.FormCode}))", ct);

        if (organizationId is Guid lodgeId &&
            !await db.Organizations.AsNoTracking().AnyAsync(
                x => x.Id == lodgeId && x.Type == "workshop", ct))
            throw new InvalidOperationException("El Taller de publicación no existe.");

        var last = await db.FormSchemaVersions.AsNoTracking()
            .Where(x => x.ScopeKey == scope && x.FormCode == next.FormCode)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        var current = last is null ? null : Decode(last);
        var validation = FormSchemaPolicy.ValidateNext(current, next, trustedCoreKeys);
        if (!validation.IsValid)
            throw new InvalidOperationException(
                "La versión no cumple las reglas de publicación: " +
                string.Join("; ", validation.Errors));

        var json = JsonSerializer.Serialize(next);
        var row = new FormSchemaVersion
        {
            ScopeKey = scope,
            FormCode = next.FormCode,
            Version = next.Version,
            EffectiveFromUtc = next.EffectiveFromUtc,
            SchemaJson = json,
            PayloadSha256 = Fingerprint(json),
            RecordedBySubject = actorSubject
        };
        db.FormSchemaVersions.Add(row);
        db.AuditEvents.Add(new AuditEvent
        {
            Action = "system.forms.schema_version_published",
            EntityType = "FormSchemaVersion",
            EntityId = row.Id.ToString("D"),
            OrganizationId = organizationId,
            ActorSubject = actorSubject,
            Menu = "Sistema",
            Submenu = "Formularios",
            Summary = "Publicación de nueva versión de formulario",
            Result = AuditResults.Success,
            CorrelationId = correlationId,
            MetadataJson = AuditMetadataSanitizer.Serialize(new
            {
                formCode = row.FormCode,
                version = row.Version,
                scope = row.ScopeKey,
                previousVersion = last?.Version ?? 0
            })
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return row;
    }

    /// <summary>
    /// Exact historical reads never silently substitute the latest version.
    /// The administrative endpoint must enforce RBAC/organization isolation.
    /// </summary>
    public async Task<FormSchemaSnapshot?> ReadAsync(
        string formCode, int version, Guid? organizationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(formCode) || version < 1)
            throw new ArgumentException("Se requiere código y versión exactos.");
        var scope = FormSchemaScope.ForOrganization(organizationId);
        var row = await db.FormSchemaVersions.AsNoTracking().SingleOrDefaultAsync(
            x => x.ScopeKey == scope && x.FormCode == formCode && x.Version == version, ct);
        return row is null ? null : Decode(row);
    }

    /// <summary>
    /// Refuse corrupted/mismatched historical definitions instead of silently
    /// rewriting or interpreting a capture using a different schema version.
    /// </summary>
    public static FormSchemaSnapshot Decode(FormSchemaVersion row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!string.Equals(Fingerprint(row.SchemaJson), row.PayloadSha256, StringComparison.Ordinal))
            throw new InvalidDataException("La definición histórica no supera su verificación de integridad.");
        try
        {
            var snapshot = JsonSerializer.Deserialize<FormSchemaSnapshot>(row.SchemaJson)
                ?? throw new InvalidDataException("La definición histórica está vacía.");
            if (snapshot.FormCode != row.FormCode ||
                snapshot.Version != row.Version ||
                snapshot.EffectiveFromUtc != row.EffectiveFromUtc ||
                snapshot.Fields is null)
                throw new InvalidDataException("Los metadatos de la versión histórica no coinciden.");
            return snapshot;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("La definición histórica no es JSON válido.", ex);
        }
    }

    public static string Fingerprint(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }
}
