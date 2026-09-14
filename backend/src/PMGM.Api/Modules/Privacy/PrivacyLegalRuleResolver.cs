using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyLegalRuleCodes
{
    public const string Prefix = "privacy.dsr.";
    public const string Suffix = ".response_days";

    public static string ResponseDays(string requestType)
        => $"{Prefix}{requestType}{Suffix}";

    public static bool IsSupportedDeadlineCode(string code)
        => PrivacyCodes.DataSubjectRight.IsValid(ExtractRight(code));

    public static string ExtractRight(string code)
    {
        if (!code.StartsWith(Prefix, StringComparison.Ordinal) ||
            !code.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return code[Prefix.Length..^Suffix.Length];
    }
}

public sealed record PrivacyLegalDeadline(
    Guid RuleSettingId,
    string RuleCode,
    int Days,
    DateOnly ReceivedDate,
    DateOnly DueDate,
    string? SourceReference);

public interface IPrivacyLegalRuleResolver
{
    Task<PrivacyLegalDeadline?> ResolveDataSubjectRequestDeadlineAsync(
        string requestType,
        DateOnly receivedDate,
        CancellationToken cancellationToken = default);
}

public sealed class PrivacyLegalRuleResolver(PmgmDbContext db) : IPrivacyLegalRuleResolver
{
    public async Task<PrivacyLegalDeadline?> ResolveDataSubjectRequestDeadlineAsync(
        string requestType,
        DateOnly receivedDate,
        CancellationToken cancellationToken = default)
    {
        if (!PrivacyCodes.DataSubjectRight.IsValid(requestType))
        {
            return null;
        }

        var code = PrivacyLegalRuleCodes.ResponseDays(requestType);
        var rule = await db.InstitutionalRuleSettings
            .AsNoTracking()
            .Where(x =>
                x.Code == code &&
                x.Status == PrivacyCodes.Status.Active &&
                x.EffectiveFrom <= receivedDate &&
                (x.EffectiveTo == null || x.EffectiveTo >= receivedDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (rule is null ||
            !int.TryParse(rule.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var days) ||
            days < 0)
        {
            return null;
        }

        return new PrivacyLegalDeadline(
            rule.Id,
            rule.Code,
            days,
            receivedDate,
            PrivacyDeadlineCalculator.Calculate(receivedDate, days),
            rule.SourceReference);
    }
}

public static class PrivacyDeadlineCalculator
{
    public static DateOnly Calculate(DateOnly receivedDate, int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        return receivedDate.AddDays(days);
    }

    public static DateOnly ToChileDate(DateTimeOffset value)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var local = TimeZoneInfo.ConvertTime(value, timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }
}
