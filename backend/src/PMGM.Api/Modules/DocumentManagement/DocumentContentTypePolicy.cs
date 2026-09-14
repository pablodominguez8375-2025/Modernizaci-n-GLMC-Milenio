using System.IO.Compression;
using System.Text;

namespace PMGM.Api.Modules.DocumentManagement;

public static class DocumentContentTypePolicy
{
    private enum SignatureKind
    {
        Pdf,
        Png,
        Jpeg,
        Zip,
        Text,
        Docx,
        Xlsx,
        Pptx
    }

    private sealed record ContentTypeRule(SignatureKind Signature, string[] Extensions);

    private static readonly IReadOnlyDictionary<string, ContentTypeRule> Rules =
        new Dictionary<string, ContentTypeRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = new(SignatureKind.Pdf, [".pdf"]),
            ["image/png"] = new(SignatureKind.Png, [".png"]),
            ["image/jpeg"] = new(SignatureKind.Jpeg, [".jpg", ".jpeg"]),
            ["application/zip"] = new(SignatureKind.Zip, [".zip"]),
            ["text/plain"] = new(SignatureKind.Text, [".txt"]),
            ["text/markdown"] = new(SignatureKind.Text, [".md", ".markdown"]),
            ["text/csv"] = new(SignatureKind.Text, [".csv"]),
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] =
                new(SignatureKind.Docx, [".docx"]),
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] =
                new(SignatureKind.Xlsx, [".xlsx"]),
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] =
                new(SignatureKind.Pptx, [".pptx"])
        };

    public static string? Normalize(string? value)
    {
        var normalized = value?.Split(';', 2, StringSplitOptions.TrimEntries)[0].Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToLowerInvariant();
    }

    public static bool TryValidateMetadata(
        string fileName,
        string? contentType,
        out string normalizedContentType,
        out string? error)
    {
        normalizedContentType = Normalize(contentType) ?? string.Empty;
        if (!Rules.TryGetValue(normalizedContentType, out var rule))
        {
            error = "El tipo MIME no está permitido para almacenamiento documental.";
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) ||
            !rule.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            error = "La extensión del archivo no corresponde al tipo MIME declarado.";
            return false;
        }

        error = null;
        return true;
    }

    public static async Task<bool> MatchesContentAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(contentType);
        if (normalized is null || !Rules.TryGetValue(normalized, out var rule)) return false;

        return rule.Signature switch
        {
            SignatureKind.Docx => await MatchesOfficeOpenXmlAsync(content, "word/", cancellationToken),
            SignatureKind.Xlsx => await MatchesOfficeOpenXmlAsync(content, "xl/", cancellationToken),
            SignatureKind.Pptx => await MatchesOfficeOpenXmlAsync(content, "ppt/", cancellationToken),
            _ => await MatchesSimpleSignatureAsync(content, rule.Signature, cancellationToken)
        };
    }

    private static async Task<bool> MatchesSimpleSignatureAsync(
        Stream content,
        SignatureKind signatureKind,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        if (read == 0) return false;

        return signatureKind switch
        {
            SignatureKind.Pdf => StartsWith(buffer, read, "%PDF-"u8),
            SignatureKind.Png => StartsWith(buffer, read, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            SignatureKind.Jpeg => StartsWith(buffer, read, [0xFF, 0xD8, 0xFF]),
            SignatureKind.Zip => IsZipSignature(buffer, read),
            SignatureKind.Text => IsUtf8Text(buffer, read),
            _ => false
        };
    }

    private static async Task<bool> MatchesOfficeOpenXmlAsync(
        Stream content,
        string requiredRoot,
        CancellationToken cancellationToken)
    {
        MemoryStream? copy = null;
        Stream archiveStream = content;

        if (!content.CanSeek)
        {
            copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            copy.Position = 0;
            archiveStream = copy;
        }
        else
        {
            content.Position = 0;
        }

        try
        {
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            return archive.GetEntry("[Content_Types].xml") is not null &&
                   archive.Entries.Any(entry =>
                       entry.FullName.StartsWith(requiredRoot, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            copy?.Dispose();
        }
    }

    private static bool StartsWith(byte[] buffer, int length, ReadOnlySpan<byte> signature)
    {
        if (length < signature.Length) return false;
        return buffer.AsSpan(0, signature.Length).SequenceEqual(signature);
    }

    private static bool IsZipSignature(byte[] buffer, int length)
    {
        if (length < 4 || buffer[0] != 0x50 || buffer[1] != 0x4B) return false;
        return (buffer[2], buffer[3]) is (0x03, 0x04) or (0x05, 0x06) or (0x07, 0x08);
    }

    private static bool IsUtf8Text(byte[] buffer, int length)
    {
        if (buffer.AsSpan(0, length).Contains((byte)0)) return false;

        try
        {
            _ = new UTF8Encoding(false, true).GetString(buffer, 0, length);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
