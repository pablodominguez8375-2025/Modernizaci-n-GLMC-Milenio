using System.Text;

namespace PMGM.Api.Modules.SecretariatOperations;

public static class SecretariatOperationsPolicy
{
    public static string? NormalizeRut(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var raw = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (raw.Length < 2) return null;
        return $"{raw[..^1]}-{raw[^1]}";
    }

    public static bool IsValidRut(string? value)
    {
        var normalized = NormalizeRut(value);
        if (normalized is null) return false;
        var parts = normalized.Split('-', 2);
        if (!int.TryParse(parts[0], out var number)) return false;
        var expected = ComputeVerifier(number);
        return string.Equals(expected, parts[1], StringComparison.OrdinalIgnoreCase);
    }

    public static string ComputeVerifier(int rut)
    {
        var sum = 0;
        var multiplier = 2;
        while (rut > 0)
        {
            sum += (rut % 10) * multiplier;
            rut /= 10;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }

        var remainder = 11 - (sum % 11);
        return remainder switch { 11 => "0", 10 => "K", _ => remainder.ToString() };
    }

    public static bool HasChronologicalMilestones(
        DateOnly? initiation,
        DateOnly? wageIncrease,
        DateOnly? exaltation)
    {
        if (initiation is not null && wageIncrease is not null && wageIncrease < initiation) return false;
        if (wageIncrease is not null && exaltation is not null && exaltation < wageIncrease) return false;
        if (initiation is not null && exaltation is not null && exaltation < initiation) return false;
        return true;
    }

    public static bool WorkPaperAllowed(string recordType, string? ceremonyType)
        => recordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
           !SecretariatOperationsCodes.CeremonyType.IsCeremonial(ceremonyType);

    public static bool WorkPaperRequiredToMarkHeld(string? ceremonyType)
        => !SecretariatOperationsCodes.CeremonyType.IsCeremonial(ceremonyType);
}
