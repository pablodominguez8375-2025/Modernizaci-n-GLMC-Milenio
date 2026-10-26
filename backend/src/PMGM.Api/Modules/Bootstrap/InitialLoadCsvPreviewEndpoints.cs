using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap.Entities;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// #694: protected, bounded and ephemeral validation of the three uploaded CSV
/// texts. Juridical review is external to the software, not an API permission.
/// This phase NEVER imports rows or changes the load window. In particular,
/// passing this preview cannot be used as an authorization to apply.
/// </summary>
public static class InitialLoadCsvPreviewEndpoints
{
    private const int MaxRequestBytes = 2_500_000;
    private const int ReadBufferBytes = 16_384;

    public static async Task<IResult> PreviewAsync(
        HttpContext context,
        IInstitutionalAccessService access,
        BootstrapDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.Pragma = "no-cache";

        // Only a server-registered unique Master with issuer-bound JWT may
        // handle personal data. Roles in a forged HTTP body are never trusted.
        var authentication = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null ||
            !InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
                authentication.Principal, access, configuration))
            return Results.Forbid();

        // Once closed, the initial-load channel is permanently sealed.
        // A nonpersistent dry-run is possible before opening; a real import is not.
        var window = await InitialLoadWindowStatusReader.ReadAsync(db, cancellationToken);
        if (window.Status == InitialLoadWindowStatusReader.Unavailable)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (window.Status == InitialLoadWindow.Closed)
            return Results.Conflict(new { code = "INITIAL_LOAD_CLOSED" });

        if (context.Request.ContentType is null ||
            !context.Request.ContentType.StartsWith(
                "application/json", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (context.Request.ContentLength is > MaxRequestBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        // Never log the body. A bounded read also guards chunked transfers
        // where Content-Length was omitted by the client.
        await using var body = new MemoryStream();
        var buffer = new byte[ReadBufferBytes];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (body.Length + read > MaxRequestBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            body.Write(buffer, 0, read);
        }

        Dictionary<string, string>? files;
        try
        {
            files = JsonSerializer.Deserialize<Dictionary<string, string>>(
                body.ToArray(), new JsonSerializerOptions { MaxDepth = 4 });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { code = "INVALID_CSV_PAYLOAD" });
        }
        if (files is null)
            return Results.BadRequest(new { code = "INVALID_CSV_PAYLOAD" });

        // Codes, field names and row indices only; NEVER echo cell values.
        // No temporary file, persisted draft, row write or credential required.
        var result = InitialLoadCsvPreviewPolicy.Inspect(files);
        var findings = result.Findings.ToList();
        var valid = result.Valid;
        if (valid)
        {
            foreach (var sheet in new[] { "Talleres.csv", "Hermanos.csv" })
            {
                if (!result.DataRowCounts.TryGetValue(sheet, out var count) || count == 0)
                {
                    valid = false;
                    findings.Add(new InitialLoadCsvPreviewPolicy.Finding(
                        sheet, 1, "rows", "REQUIRED_ROWS"));
                }
            }
        }

        return Results.Ok(new
        {
            valid,
            canApply = false, // No transactional import endpoint yet.
            writesPerformed = 0,
            status = valid ? "PREVIEW_VALID" : "PREVIEW_REQUIRES_CORRECTION",
            dataRowCounts = result.DataRowCounts,
            findings
        });
    }
}
