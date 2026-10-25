using System.Buffers.Binary;
using PMGM.Api.Modules.CandidateIntake;

namespace PMGM.Api.Modules.Membership;

/// <summary>
/// Foto de perfil del Hermano (#609). El cliente recorta y reduce a un cuadrado; el servidor NO confía en eso:
/// valida el contenido real (firma, estructura, dimensiones), exige formato cuadrado y elimina los metadatos
/// (EXIF/GPS, XMP, comentarios, perfiles) antes de guardar.
/// </summary>
public static class MemberPhotoPolicy
{
    public const int MaxBytes = 400 * 1024;
    public const int MinSide = 200;
    public const int MaxSide = 1200;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly HashSet<string> KeptPngChunks = ["IHDR", "PLTE", "tRNS", "IDAT", "IEND"];

    public static string? ContentTypeOf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(PngSignature)) return "image/png";
        return null;
    }

    /// <summary>Devuelve el contenido limpio, o un mensaje de rechazo.</summary>
    public static (byte[]? Content, string? ContentType, int Width, int Height, string? Error) Normalize(byte[] bytes)
    {
        if (bytes.Length == 0) return (null, null, 0, 0, "Seleccione una imagen.");
        if (bytes.Length > MaxBytes) return (null, null, 0, 0, "La foto supera el tamaño máximo (400 KB). Use una imagen más liviana.");
        var type = ContentTypeOf(bytes);
        if (type is null) return (null, null, 0, 0, "La foto debe ser JPG o PNG.");
        if (!CandidatePhotoPolicy.TryReadDimensions(bytes, type, out var width, out var height))
            return (null, null, 0, 0, "No fue posible leer la imagen. Use otro archivo JPG o PNG.");
        if (width != height) return (null, null, 0, 0, "La foto debe ser cuadrada (recórtela antes de guardar).");
        if (width < MinSide || width > MaxSide) return (null, null, 0, 0, $"La foto debe medir entre {MinSide} y {MaxSide} píxeles por lado.");
        var cleaned = type == "image/jpeg" ? StripJpeg(bytes) : StripPng(bytes);
        if (cleaned is null) return (null, null, 0, 0, "La imagen está dañada o tiene una estructura no válida.");
        return (cleaned, type, width, height, null);
    }

    /// <summary>Conserva solo los segmentos necesarios para decodificar (APP0/JFIF, tablas, marco, escaneo); quita APP1..APP15 y comentarios.</summary>
    internal static byte[]? StripJpeg(byte[] source)
    {
        using var output = new MemoryStream(source.Length);
        output.Write(source, 0, 2); // SOI
        var offset = 2;
        while (offset + 1 < source.Length)
        {
            if (source[offset] != 0xFF) return null;
            var marker = source[offset + 1];
            if (marker == 0xFF) { offset++; continue; }
            if (marker == 0xD9) { output.Write(source, offset, 2); return output.ToArray(); }
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7) { output.Write(source, offset, 2); offset += 2; continue; }
            if (offset + 4 > source.Length) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(offset + 2, 2));
            if (length < 2 || offset + 2 + length > source.Length) return null;
            var drop = marker is >= 0xE1 and <= 0xEF || marker == 0xFE;
            if (marker == 0xDA)
            {
                // Desde el inicio del escaneo todo es dato comprimido hasta EOI: se copia tal cual.
                output.Write(source, offset, source.Length - offset);
                return output.ToArray();
            }
            if (!drop) output.Write(source, offset, 2 + length);
            offset += 2 + length;
        }
        return null;
    }

    /// <summary>Conserva solo los bloques críticos (IHDR, PLTE, tRNS, IDAT, IEND); descarta texto, EXIF (eXIf), perfiles y fechas.</summary>
    internal static byte[]? StripPng(byte[] source)
    {
        using var output = new MemoryStream(source.Length);
        output.Write(source, 0, 8);
        var offset = 8;
        var sawEnd = false;
        while (offset + 12 <= source.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4));
            if (length < 0 || offset + 12L + length > source.Length) return null;
            var name = System.Text.Encoding.ASCII.GetString(source, offset + 4, 4);
            if (KeptPngChunks.Contains(name)) output.Write(source, offset, 12 + length);
            offset += 12 + length;
            if (name == "IEND") { sawEnd = true; break; }
        }
        return sawEnd ? output.ToArray() : null;
    }
}
