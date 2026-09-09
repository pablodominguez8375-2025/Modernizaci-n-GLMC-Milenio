using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Bootstrap.Entities;
using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Bootstrap;

public interface IInstitutionalBootstrapService
{
    Task<BootstrapPlanResponse> PlanAsync(InstitutionalBootstrapRequest request, CancellationToken cancellationToken);
    Task<BootstrapApplyResponse> ApplyAsync(HttpContext httpContext, InstitutionalBootstrapRequest request, CancellationToken cancellationToken);
    Task<BootstrapCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken);
}

public sealed class InstitutionalBootstrapService(BootstrapDbContext db) : IInstitutionalBootstrapService
{
    private static readonly Regex PackageKeyPattern = new("^[a-z0-9][a-z0-9._-]{2,159}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<BootstrapPlanResponse> PlanAsync(
        InstitutionalBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var hash = ComputeHash(normalized);
        var errors = ValidateShape(normalized);
        if (errors.Count > 0)
            return BootstrapPlanResponse.Invalid(hash, errors);

        var existingApplication = await db.BootstrapApplications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.PackageKey == normalized.PackageKey && x.PackageVersion == normalized.PackageVersion,
                cancellationToken);

        if (existingApplication is not null)
        {
            if (!string.Equals(existingApplication.PayloadSha256, hash, StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapPlanResponse.Invalid(hash, new[]
                {
                    "El mismo packageKey/packageVersion ya fue aplicado con un contenido diferente. Use una nueva versión del paquete."
                });
            }

            var alreadyCatalog = await BuildCatalogAsync(cancellationToken);
            return new BootstrapPlanResponse(
                true,
                true,
                hash,
                Array.Empty<string>(),
                new BootstrapChangeSummary(0, 0, 0, 0, 0, 0),
                alreadyCatalog);
        }

        var plan = await AnalyzeStateAsync(normalized, cancellationToken);
        if (plan.Errors.Count > 0)
            return BootstrapPlanResponse.Invalid(hash, plan.Errors);

        return new BootstrapPlanResponse(
            true,
            false,
            hash,
            Array.Empty<string>(),
            plan.Changes,
            plan.Catalog);
    }

    public async Task<BootstrapApplyResponse> ApplyAsync(
        HttpContext httpContext,
        InstitutionalBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var hash = ComputeHash(normalized);
        var shapeErrors = ValidateShape(normalized);
        if (shapeErrors.Count > 0)
            return BootstrapApplyResponse.Invalid(hash, shapeErrors);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var existingApplication = await db.BootstrapApplications
            .SingleOrDefaultAsync(
                x => x.PackageKey == normalized.PackageKey && x.PackageVersion == normalized.PackageVersion,
                cancellationToken);

        if (existingApplication is not null)
        {
            if (!string.Equals(existingApplication.PayloadSha256, hash, StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapApplyResponse.Invalid(hash, new[]
                {
                    "El mismo packageKey/packageVersion ya fue aplicado con un contenido diferente. Use una nueva versión del paquete."
                });
            }

            await transaction.RollbackAsync(cancellationToken);
            var existingOrganizations = await ResolveOrganizationsAsync(normalized, cancellationToken);
            var catalog = await BuildCatalogAsync(cancellationToken);
            return new BootstrapApplyResponse(
                true,
                true,
                hash,
                Array.Empty<string>(),
                existingOrganizations.Institution,
                existingOrganizations.Workshops,
                catalog);
        }

        var plan = await AnalyzeStateAsync(normalized, cancellationToken);
        if (plan.Errors.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BootstrapApplyResponse.Invalid(hash, plan.Errors);
        }

        var institution = await db.Organizations
            .SingleOrDefaultAsync(x => x.Type == BootstrapCodes.GrandLodgeType, cancellationToken);
        if (institution is null)
        {
            institution = new Organization
            {
                Name = normalized.Institution.Name,
                Type = BootstrapCodes.GrandLodgeType,
                Number = null
            };
            db.Organizations.Add(institution);
            await db.SaveChangesAsync(cancellationToken);
        }

        var workshops = new List<Organization>();
        foreach (var workshopRequest in normalized.Workshops)
        {
            var workshop = await db.Organizations.SingleOrDefaultAsync(
                x => x.Type == BootstrapCodes.WorkshopType && x.Number == workshopRequest.Number,
                cancellationToken);

            if (workshop is null)
            {
                workshop = new Organization
                {
                    Name = workshopRequest.Name,
                    Number = workshopRequest.Number,
                    Type = BootstrapCodes.WorkshopType,
                    ParentOrganizationId = institution.Id
                };
                db.Organizations.Add(workshop);
            }
            workshops.Add(workshop);
        }

        await EnsureCatalogsAsync(cancellationToken);

        var subject = httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var summary = new
        {
            institution = institution.Id,
            workshops = workshops.Select(x => x.Id).ToArray(),
            securityProfiles = BootstrapCodes.SecurityProfiles.Count,
            officeDefinitions = BootstrapCodes.WorkshopOffices.Count
        };

        db.BootstrapApplications.Add(new InstitutionalBootstrapApplication
        {
            PackageKey = normalized.PackageKey,
            PackageVersion = normalized.PackageVersion,
            PayloadSha256 = hash,
            Status = BootstrapCodes.ApplicationStatus.Applied,
            AppliedBySubject = subject,
            SummaryJson = JsonSerializer.Serialize(summary, JsonOptions)
        });

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "institutional_bootstrap.apply",
            "InstitutionalBootstrap",
            $"{normalized.PackageKey}:{normalized.PackageVersion}",
            institution.Id,
            AuditResults.Success,
            new
            {
                normalized.PackageKey,
                normalized.PackageVersion,
                WorkshopCount = workshops.Count,
                SecurityProfileCount = BootstrapCodes.SecurityProfiles.Count,
                OfficeDefinitionCount = BootstrapCodes.WorkshopOffices.Count
            }));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var catalogAfterApply = await BuildCatalogAsync(cancellationToken);
        return new BootstrapApplyResponse(
            true,
            false,
            hash,
            Array.Empty<string>(),
            ToDto(institution),
            workshops.OrderBy(x => x.Number).Select(ToDto).ToArray(),
            catalogAfterApply);
    }

    public Task<BootstrapCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken)
        => BuildCatalogAsync(cancellationToken);

    private async Task<BootstrapStateAnalysis> AnalyzeStateAsync(
        InstitutionalBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var institutionCreated = 0;
        var institutionUnchanged = 0;
        var workshopCreated = 0;
        var workshopUnchanged = 0;
        var securityCreated = 0;
        var officesCreated = 0;

        var institutions = await db.Organizations
            .AsNoTracking()
            .Where(x => x.Type == BootstrapCodes.GrandLodgeType)
            .ToListAsync(cancellationToken);

        if (institutions.Count > 1)
        {
            errors.Add("La base contiene más de una institución matriz de tipo grand_lodge. Se requiere revisión antes del bootstrap.");
        }
        else if (institutions.Count == 1)
        {
            var existing = institutions[0];
            if (!Same(existing.Name, request.Institution.Name))
                errors.Add($"La institución matriz existente no coincide con el paquete: '{existing.Name}'.");
            else
                institutionUnchanged++;
        }
        else
        {
            institutionCreated++;
        }

        var knownInstitutionId = institutions.Count == 1 ? institutions[0].Id : (Guid?)null;
        foreach (var workshop in request.Workshops)
        {
            var existing = await db.Organizations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Type == BootstrapCodes.WorkshopType && x.Number == workshop.Number,
                    cancellationToken);

            if (existing is null)
            {
                workshopCreated++;
                continue;
            }

            if (!Same(existing.Name, workshop.Name))
                errors.Add($"El Taller Nº {workshop.Number} ya existe con un nombre diferente: '{existing.Name}'.");
            else if (knownInstitutionId is not null && existing.ParentOrganizationId != knownInstitutionId)
                errors.Add($"El Taller Nº {workshop.Number} no pertenece a la institución matriz detectada.");
            else
                workshopUnchanged++;
        }

        var existingSecurity = await db.SecurityProfiles.AsNoTracking().ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var seed in BootstrapCodes.SecurityProfiles)
        {
            if (!existingSecurity.TryGetValue(seed.Code, out var profile))
            {
                securityCreated++;
                continue;
            }

            if (!Same(profile.Name, seed.Name) || !Same(profile.Scope, seed.Scope) || !Same(profile.Category, seed.Category))
                errors.Add($"El perfil de seguridad '{seed.Code}' existe con una definición incompatible.");
        }

        var existingOffices = await db.OfficeDefinitions.AsNoTracking().ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var seed in BootstrapCodes.WorkshopOffices)
        {
            if (!existingOffices.TryGetValue(seed.Code, out var office))
            {
                officesCreated++;
                continue;
            }

            if (!Same(office.Name, seed.Name) || !Same(office.Category, seed.Category) || office.IsPrimary != seed.IsPrimary)
                errors.Add($"El cargo institucional '{seed.Code}' existe con una definición incompatible.");
        }

        return new BootstrapStateAnalysis(
            errors,
            new BootstrapChangeSummary(
                institutionCreated,
                institutionUnchanged,
                workshopCreated,
                workshopUnchanged,
                securityCreated,
                officesCreated),
            await BuildCatalogAsync(cancellationToken));
    }

    private async Task EnsureCatalogsAsync(CancellationToken cancellationToken)
    {
        var existingSecurity = await db.SecurityProfiles.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var seed in BootstrapCodes.SecurityProfiles)
        {
            if (existingSecurity.ContainsKey(seed.Code)) continue;
            db.SecurityProfiles.Add(new SecurityProfileDefinition
            {
                Code = seed.Code,
                Name = seed.Name,
                Scope = seed.Scope,
                Category = seed.Category,
                Description = seed.Description,
                IsSystem = seed.Category != BootstrapCodes.SecurityCategory.Support,
                IsActive = true
            });
        }

        var existingOffices = await db.OfficeDefinitions.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var seed in BootstrapCodes.WorkshopOffices)
        {
            if (existingOffices.ContainsKey(seed.Code)) continue;
            db.OfficeDefinitions.Add(new OfficeDefinition
            {
                Code = seed.Code,
                Name = seed.Name,
                Category = seed.Category,
                OrganizationType = BootstrapCodes.WorkshopType,
                AliasesJson = seed.Aliases is null ? null : JsonSerializer.Serialize(seed.Aliases, JsonOptions),
                IsPrimary = seed.IsPrimary,
                IsActive = true,
                SortOrder = seed.SortOrder
            });
        }
    }

    private async Task<BootstrapCatalogResponse> BuildCatalogAsync(CancellationToken cancellationToken)
    {
        var profiles = await db.SecurityProfiles
            .AsNoTracking()
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.Name)
            .Select(x => new SecurityProfileDto(x.Code, x.Name, x.Scope, x.Category, x.Description, x.IsSystem, x.IsActive))
            .ToListAsync(cancellationToken);

        var offices = await db.OfficeDefinitions
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new OfficeDefinitionDto(x.Code, x.Name, x.Category, x.IsPrimary, x.IsActive, x.AliasesJson))
            .ToListAsync(cancellationToken);

        return new BootstrapCatalogResponse(profiles, offices);
    }

    private async Task<ResolvedOrganizations> ResolveOrganizationsAsync(
        InstitutionalBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        var institution = await db.Organizations
            .AsNoTracking()
            .SingleAsync(x => x.Type == BootstrapCodes.GrandLodgeType, cancellationToken);
        var numbers = request.Workshops.Select(x => x.Number).ToArray();
        var workshops = await db.Organizations
            .AsNoTracking()
            .Where(x => x.Type == BootstrapCodes.WorkshopType && x.Number != null && numbers.Contains(x.Number))
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);

        return new ResolvedOrganizations(ToDto(institution), workshops.Select(ToDto).ToArray());
    }

    private static InstitutionalBootstrapRequest Normalize(InstitutionalBootstrapRequest request)
        => new(
            request.PackageKey.Trim().ToLowerInvariant(),
            request.PackageVersion,
            new BootstrapInstitution(request.Institution.Name.Trim()),
            request.Workshops
                .Select(x => new BootstrapWorkshop(x.Name.Trim(), x.Number.Trim()))
                .OrderBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());

    private static IReadOnlyList<string> ValidateShape(InstitutionalBootstrapRequest request)
    {
        var errors = new List<string>();
        if (!PackageKeyPattern.IsMatch(request.PackageKey))
            errors.Add("packageKey debe tener entre 3 y 160 caracteres y usar sólo minúsculas, números, punto, guion o guion bajo.");
        if (request.PackageVersion < 1)
            errors.Add("packageVersion debe ser mayor o igual a 1.");
        if (string.IsNullOrWhiteSpace(request.Institution.Name) || request.Institution.Name.Length > 200)
            errors.Add("La institución debe tener un nombre entre 1 y 200 caracteres.");
        if (request.Workshops.Count == 0)
            errors.Add("El paquete debe contener al menos un Taller.");
        if (request.Workshops.Count > 500)
            errors.Add("El paquete no puede contener más de 500 Talleres.");

        var duplicateNumbers = request.Workshops
            .GroupBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();
        if (duplicateNumbers.Length > 0)
            errors.Add($"Hay números de Taller duplicados en el paquete: {string.Join(", ", duplicateNumbers)}.");

        foreach (var workshop in request.Workshops)
        {
            if (string.IsNullOrWhiteSpace(workshop.Name) || workshop.Name.Length > 200)
                errors.Add("Cada Taller debe tener un nombre entre 1 y 200 caracteres.");
            if (string.IsNullOrWhiteSpace(workshop.Number) || workshop.Number.Length > 40)
                errors.Add("Cada Taller debe tener un número entre 1 y 40 caracteres.");
        }

        return errors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ComputeHash(InstitutionalBootstrapRequest request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static bool Same(string left, string right)
        => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static OrganizationBootstrapDto ToDto(Organization organization)
        => new(organization.Id, organization.Name, organization.Number, organization.Type, organization.ParentOrganizationId);

    private sealed record BootstrapStateAnalysis(
        IReadOnlyList<string> Errors,
        BootstrapChangeSummary Changes,
        BootstrapCatalogResponse Catalog);

    private sealed record ResolvedOrganizations(
        OrganizationBootstrapDto Institution,
        IReadOnlyList<OrganizationBootstrapDto> Workshops);
}

public sealed record InstitutionalBootstrapRequest(
    string PackageKey,
    int PackageVersion,
    BootstrapInstitution Institution,
    IReadOnlyList<BootstrapWorkshop> Workshops);

public sealed record BootstrapInstitution(string Name);
public sealed record BootstrapWorkshop(string Name, string Number);

public sealed record BootstrapChangeSummary(
    int InstitutionsToCreate,
    int InstitutionsUnchanged,
    int WorkshopsToCreate,
    int WorkshopsUnchanged,
    int SecurityProfilesToCreate,
    int OfficeDefinitionsToCreate);

public sealed record BootstrapPlanResponse(
    bool Valid,
    bool AlreadyApplied,
    string PayloadSha256,
    IReadOnlyList<string> Errors,
    BootstrapChangeSummary Changes,
    BootstrapCatalogResponse Catalog)
{
    public static BootstrapPlanResponse Invalid(string hash, IReadOnlyList<string> errors)
        => new(false, false, hash, errors, new BootstrapChangeSummary(0, 0, 0, 0, 0, 0), new BootstrapCatalogResponse(Array.Empty<SecurityProfileDto>(), Array.Empty<OfficeDefinitionDto>()));
}

public sealed record BootstrapApplyResponse(
    bool Applied,
    bool AlreadyApplied,
    string PayloadSha256,
    IReadOnlyList<string> Errors,
    OrganizationBootstrapDto? Institution,
    IReadOnlyList<OrganizationBootstrapDto> Workshops,
    BootstrapCatalogResponse Catalog)
{
    public static BootstrapApplyResponse Invalid(string hash, IReadOnlyList<string> errors)
        => new(false, false, hash, errors, null, Array.Empty<OrganizationBootstrapDto>(), new BootstrapCatalogResponse(Array.Empty<SecurityProfileDto>(), Array.Empty<OfficeDefinitionDto>()));
}

public sealed record BootstrapCatalogResponse(
    IReadOnlyList<SecurityProfileDto> SecurityProfiles,
    IReadOnlyList<OfficeDefinitionDto> OfficeDefinitions);

public sealed record SecurityProfileDto(
    string Code,
    string Name,
    string Scope,
    string Category,
    string Description,
    bool IsSystem,
    bool IsActive);

public sealed record OfficeDefinitionDto(
    string Code,
    string Name,
    string Category,
    bool IsPrimary,
    bool IsActive,
    string? AliasesJson);

public sealed record OrganizationBootstrapDto(
    Guid Id,
    string Name,
    string? Number,
    string Type,
    Guid? ParentOrganizationId);
