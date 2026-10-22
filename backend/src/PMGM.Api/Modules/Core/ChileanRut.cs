using System.Text.RegularExpressions;

namespace PMGM.Api.Modules.Core;

/// <summary>
/// Formatea y valida RUT/RUN chilenos con módulo 11. La validez matemática
/// NO prueba existencia, inscripción ni titularidad.
/// </summary>
public static class ChileanRut
{
    public static bool TryNormalize(string? input, out string normalized, out string formatted)
    {
        normalized = string.Empty;
        formatted = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var value = input.Trim().ToUpperInvariant();
        var raw = Regex.Match(value, @"^(\d{1,8})-?([0-9K])$", RegexOptions.CultureInvariant);
        var grouped = raw.Success ? null : Regex.Match(
            value, @"^(\d{1,3}(?:\.\d{3}){1,2})-?([0-9K])$", RegexOptions.CultureInvariant);
        var bodyInput = raw.Success ? raw.Groups[1].Value
            : grouped?.Success == true ? grouped.Groups[1].Value.Replace(".", string.Empty) : null;
        var digit = raw.Success ? raw.Groups[2].Value
            : grouped?.Success == true ? grouped.Groups[2].Value : null;
        if (bodyInput is null || digit is null || bodyInput.Length > 8) return false;

        var body = bodyInput.TrimStart('0');
        if (body.Length == 0) return false;
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
            sum += (body[body.Length - 1 - i] - '0') * (2 + i % 6);

        var verification = 11 - sum % 11;
        var expected = verification == 11 ? "0" : verification == 10 ? "K" : verification.ToString();
        if (digit != expected) return false;

        normalized = body + "-" + digit;
        formatted = Regex.Replace(body, @"\B(?=(\d{3})+(?!\d))", ".") + "-" + digit;
        return true;
    }

    public static bool IsValid(string? value) => TryNormalize(value, out _, out _);
}
