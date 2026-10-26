using System.IO.Compression;
using System.Text;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Initial-load CSV contract and empty template. Preview accepts bounded CSV
/// content for the registered Master only, without storing or applying rows.
/// Institutional juridical review happens OUTSIDE this application. Actual
/// import still requires transactional authorization, idempotency and audit.
/// </summary>
public static class InitialLoadTemplateEndpoints
{
    public sealed record CsvSheet(string Name, string[] Columns);

    public static readonly CsvSheet[] Sheets =
    [
        new("Talleres.csv", ["workshopCode", "number", "name", "orienteCode", "city", "country"]),
        new("Hermanos.csv",
            ["personRef", "identityType", "identityNumber", "firstNames", "lastNames",
             "email", "workshopCode", "grade", "institutionalStatus"]),
        new("CargosVigentes.csv", ["personRef", "workshopCode", "officeCode", "startDate"])
    ];

    public static IEndpointRouteBuilder MapInitialLoadTemplateEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/platform/bootstrap/initial-load/contract", GetContract)
            .RequireAuthorization()
            .WithTags("Bootstrap institucional — Carga inicial");
        endpoints.MapGet("/api/platform/bootstrap/initial-load/template-csv", DownloadCsvTemplate)
            .RequireAuthorization()
            .WithTags("Bootstrap institucional — Carga inicial");
        endpoints.MapPost("/api/platform/bootstrap/initial-load/preview",
                InitialLoadCsvPreviewEndpoints.PreviewAsync)
            .RequireAuthorization()
            .WithTags("Bootstrap institucional — Carga inicial");
        return endpoints;
    }

    private static IResult GetContract(HttpContext context, IInstitutionalAccessService access)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!access.IsPlatformSuperAdmin(context.User))
            return Results.Forbid();

        return Results.Ok(new
        {
            status = "PREVIEW_ONLY",
            revision = "INITIAL-LOAD-CSV-v1",
            files = Sheets.Select(s => new { name = s.Name, columns = s.Columns }),
            requiresLegalApprovalBeforeRealData = false,
            legalReviewIsExternal = true,
            acceptsUploadedData = true,
            createsAccounts = false,
            assignsProfiles = false,
            supportsApply = false
        });
    }

    private static IResult DownloadCsvTemplate(
        HttpContext context, IInstitutionalAccessService access)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!access.IsPlatformSuperAdmin(context.User))
            return Results.Forbid();

        return Results.File(CreateEmptyCsvArchive(), "application/zip",
            fileDownloadName: "PMGM-Carga-Inicial-Plantillas-CSV-v1.zip");
    }

    /// <summary>
    /// Three named header-only UTF-8 CSV files, deterministic content.
    /// No personal data, examples or formula values. No filesystem writes.
    /// </summary>
    public static byte[] CreateEmptyCsvArchive()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var sheet in Sheets)
            {
                var entry = zip.CreateEntry(sheet.Name, CompressionLevel.Optimal);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(string.Join(",", sheet.Columns));
                writer.Write("\r\n");
            }
        }
        return output.ToArray();
    }
}
