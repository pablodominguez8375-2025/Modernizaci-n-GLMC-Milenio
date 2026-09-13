using System.Buffers.Binary;

namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidatePhotoPolicy
{
    public const long MaxBytes = 100 * 1024;
    public const int MinimumWidth = 500;
    public const int MinimumHeight = 500;

    public static bool TryReadDimensions(ReadOnlySpan<byte> content, string contentType, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            if (content.Length < 24 || !content[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return false;
            width = BinaryPrimitives.ReadInt32BigEndian(content.Slice(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(content.Slice(20, 4));
            return width > 0 && height > 0;
        }

        if (!contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) || content.Length < 4 || content[0] != 0xff || content[1] != 0xd8) return false;
        var offset = 2;
        while (offset + 8 < content.Length)
        {
            if (content[offset] != 0xff) { offset++; continue; }
            var marker = content[offset + 1];
            offset += 2;
            if (marker is 0xd8 or 0xd9 || marker is >= 0xd0 and <= 0xd7) continue;
            if (offset + 2 > content.Length) return false;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > content.Length) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
            {
                if (segmentLength < 7) return false;
                height = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 5, 2));
                return width > 0 && height > 0;
            }
            offset += segmentLength;
        }
        return false;
    }

    public static bool MeetsMinimumDimensions(ReadOnlySpan<byte> content, string contentType, out int width, out int height)
        => TryReadDimensions(content, contentType, out width, out height) && width >= MinimumWidth && height >= MinimumHeight;
}
