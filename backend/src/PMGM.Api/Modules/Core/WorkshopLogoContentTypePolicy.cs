namespace PMGM.Api.Modules.Core;

public static class WorkshopLogoContentTypePolicy
{
    public const int MaxUploadBytes = 2 * 1024 * 1024;

    public static bool IsAllowed(string? contentType, ReadOnlySpan<byte> content)
    {
        if (string.Equals(contentType?.Trim(), "image/png", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            return content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);
        }

        if (string.Equals(contentType?.Trim(), "image/jpeg", StringComparison.OrdinalIgnoreCase))
            return content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;

        return false;
    }
}
