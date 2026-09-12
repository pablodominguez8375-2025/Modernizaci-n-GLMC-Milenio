using System.Data;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Authorization;

public sealed record InstitutionalMemberContext(Guid MemberId, int EffectiveDegree);

public interface IInstitutionalMemberContextResolver
{
    Task<InstitutionalMemberContext?> ResolveAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}

public sealed class InstitutionalMemberContextResolver(PmgmDbContext db)
    : IInstitutionalMemberContextResolver
{
    private const string UnspecifiedIssuer = "urn:pmgm:unspecified-issuer";

    public async Task<InstitutionalMemberContext?> ResolveAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var subject = user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject)) return null;

        var issuer = user.FindFirstValue("iss");
        if (string.IsNullOrWhiteSpace(issuer)) issuer = UnspecifiedIssuer;

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);

        try
        {
            var memberId = await ResolveMemberIdAsync(
                connection,
                issuer,
                subject,
                cancellationToken);
            if (memberId is null) return null;

            var effectiveDegree = await ResolveEffectiveDegreeAsync(
                connection,
                memberId.Value,
                DateOnly.FromDateTime(DateTime.UtcNow),
                cancellationToken);
            if (effectiveDegree is null) return null;

            return new InstitutionalMemberContext(memberId.Value, effectiveDegree.Value);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static async Task<Guid?> ResolveMemberIdAsync(
        DbConnection connection,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "MemberId"
            FROM core.member_identity_links
            WHERE "Issuer" = @issuer
              AND "Subject" = @subject
              AND "RevokedAtUtc" IS NULL
            LIMIT 1;
            """;
        AddParameter(command, "@issuer", issuer);
        AddParameter(command, "@subject", subject);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid memberId ? memberId : null;
    }

    private static async Task<int?> ResolveEffectiveDegreeAsync(
        DbConnection connection,
        Guid memberId,
        DateOnly effectiveDate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "Degree"
            FROM core.degree_events
            WHERE "MemberId" = @memberId
              AND "EffectiveDate" <= @effectiveDate
            ORDER BY "EffectiveDate" DESC, "RecordedAtUtc" DESC;
            """;
        AddParameter(command, "@memberId", memberId);
        AddParameter(command, "@effectiveDate", effectiveDate);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0) &&
                InstitutionalDegree.TryParse(reader.GetString(0), out var degree))
            {
                return degree;
            }
        }

        return null;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    internal static string GetUnspecifiedIssuerForTests() => UnspecifiedIssuer;
}

public static class InstitutionalDegree
{
    public static bool TryParse(string? value, out int degree)
    {
        degree = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("º", string.Empty, StringComparison.Ordinal)
            .Replace("°", string.Empty, StringComparison.Ordinal)
            .Replace("grado", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (int.TryParse(normalized, out var numeric) && numeric is >= 1 and <= 99)
        {
            degree = numeric;
            return true;
        }

        degree = normalized switch
        {
            "primero" or "primer" or "aprendiz" or "apprentice" => 1,
            "segundo" or "compañero" or "companero" or "fellowcraft" => 2,
            "tercero" or "tercer" or "maestro" or "master" => 3,
            _ => 0
        };

        return degree > 0;
    }
}
