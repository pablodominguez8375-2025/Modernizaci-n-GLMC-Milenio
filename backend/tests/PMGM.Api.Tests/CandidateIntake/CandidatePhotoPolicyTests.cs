using System.Buffers.Binary;
using PMGM.Api.Modules.CandidateIntake;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidatePhotoPolicyTests
{
    [Theory]
    [InlineData(500, 500, true)]
    [InlineData(800, 600, true)]
    [InlineData(499, 500, false)]
    [InlineData(500, 499, false)]
    public void PngDimensions_EnforceOfficialMinimum(int width, int height, bool expected)
    {
        var png = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), height);

        Assert.Equal(expected, CandidatePhotoPolicy.MeetsMinimumDimensions(png, "image/png", out _, out _));
    }

    [Fact]
    public void InvalidSignature_IsRejected()
        => Assert.False(CandidatePhotoPolicy.MeetsMinimumDimensions(new byte[24], "image/png", out _, out _));
}
