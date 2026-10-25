using System.Buffers.Binary;
using System.Text;
using PMGM.Api.Modules.Membership;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class MemberPhotoPolicyTests
{
    /// <summary>JPEG sintético mínimo (marco + escaneo) con EXIF opcional y relleno para pruebas de tamaño.</summary>
    public static byte[] Jpeg(int width, int? height = null, bool withExif = false, int padding = 0)
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xD8]);
        ms.Write([0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        if (withExif)
        {
            var exif = Encoding.ASCII.GetBytes("Exif\0\0GPS-LAT-33.45;GPS-LON-70.66");
            ms.Write([0xFF, 0xE1]); var len = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(len, (ushort)(exif.Length + 2)); ms.Write(len); ms.Write(exif);
            var comment = Encoding.ASCII.GetBytes("camara-secreta");
            ms.Write([0xFF, 0xFE]); BinaryPrimitives.WriteUInt16BigEndian(len, (ushort)(comment.Length + 2)); ms.Write(len); ms.Write(comment);
        }
        var h = height ?? width;
        var sof = new byte[] { 0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(h >> 8), (byte)h, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, 0x00 };
        ms.Write(sof);
        ms.Write([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        ms.Write(new byte[Math.Max(padding, 16)]);
        ms.Write([0xFF, 0xD9]);
        return ms.ToArray();
    }

    private static byte[] Png(int side, bool withText)
    {
        using var ms = new MemoryStream();
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        void Chunk(string name, byte[] data)
        {
            var len = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(len, data.Length); ms.Write(len);
            ms.Write(Encoding.ASCII.GetBytes(name)); ms.Write(data); ms.Write(new byte[4]); // CRC no se valida
        }
        var ihdr = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), side); BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), side); ihdr[8] = 8; ihdr[9] = 2;
        Chunk("IHDR", ihdr);
        if (withText) { Chunk("tEXt", Encoding.ASCII.GetBytes("Author\0Hermano Secreto")); Chunk("eXIf", Encoding.ASCII.GetBytes("GPS")); }
        Chunk("IDAT", [1, 2, 3, 4]);
        Chunk("IEND", []);
        return ms.ToArray();
    }

    [Fact]
    public void Jpeg_metadata_is_removed_and_image_stays_valid()
    {
        var result = MemberPhotoPolicy.Normalize(Jpeg(300, withExif: true));
        Assert.Null(result.Error);
        Assert.Equal("image/jpeg", result.ContentType);
        var text = Encoding.ASCII.GetString(result.Content!);
        Assert.DoesNotContain("Exif", text); Assert.DoesNotContain("GPS", text); Assert.DoesNotContain("camara", text);
        Assert.Contains("JFIF", text);
        Assert.Equal((byte)0xD9, result.Content![^1]);
        Assert.True(result.Content!.Length < Jpeg(300, withExif: true).Length);
    }

    [Fact]
    public void Png_ancillary_chunks_are_removed()
    {
        var result = MemberPhotoPolicy.Normalize(Png(256, withText: true));
        Assert.Null(result.Error);
        var text = Encoding.ASCII.GetString(result.Content!);
        Assert.DoesNotContain("tEXt", text); Assert.DoesNotContain("eXIf", text); Assert.DoesNotContain("Secreto", text);
        Assert.Contains("IDAT", text); Assert.Contains("IEND", text);
    }

    [Theory]
    [InlineData(300, 200, "cuadrada")]
    [InlineData(100, 100, "píxeles")]
    [InlineData(1300, 1300, "píxeles")]
    public void Non_square_or_out_of_range_images_are_rejected(int w, int h, string expected)
        => Assert.Contains(expected, MemberPhotoPolicy.Normalize(Jpeg(w, h)).Error);

    [Fact]
    public void Fake_or_truncated_files_are_rejected()
    {
        Assert.NotNull(MemberPhotoPolicy.Normalize([]).Error);
        Assert.Contains("JPG o PNG", MemberPhotoPolicy.Normalize(Encoding.ASCII.GetBytes("GIF89a....")).Error);
        var truncated = Jpeg(300); Array.Resize(ref truncated, 24);
        Assert.NotNull(MemberPhotoPolicy.Normalize(truncated).Error);
        Assert.Contains("400 KB", MemberPhotoPolicy.Normalize(Jpeg(300, padding: MemberPhotoPolicy.MaxBytes)).Error);
    }
}
