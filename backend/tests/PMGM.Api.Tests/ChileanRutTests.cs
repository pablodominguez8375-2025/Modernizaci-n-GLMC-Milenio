using PMGM.Api.Modules.Core;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class ChileanRutTests
{
    [Theory]
    [InlineData("10000013-k", "10000013-K", "10.000.013-K")]
    [InlineData("10000013K", "10000013-K", "10.000.013-K")]
    [InlineData("10.000.013-K", "10000013-K", "10.000.013-K")]
    [InlineData("12.345.678-5", "12345678-5", "12.345.678-5")]
    [InlineData("10000004-0", "10000004-0", "10.000.004-0")]
    [InlineData("9.876.543-3", "9876543-3", "9.876.543-3")]
    public void Normalize_and_format_modulo_11(string input, string expectedNormalized, string expectedFormatted)
    {
        Assert.True(ChileanRut.TryNormalize(input, out var normalized, out var formatted));
        Assert.Equal(expectedNormalized, normalized);
        Assert.Equal(expectedFormatted, formatted);
    }

    [Theory]
    [InlineData("12.345.678-K")]
    [InlineData("10.000.013-0")]
    [InlineData("0-0")]
    [InlineData("12345678")]
    [InlineData("12.34.567-5")]
    [InlineData("12--345678-5")]
    [InlineData("123456789-0")]
    [InlineData("12.345.678-X")]
    [InlineData("")]
    [InlineData(null)]
    public void Reject_wrong_digit_and_invalid_shapes(string? input)
    {
        Assert.False(ChileanRut.IsValid(input));
        Assert.False(ChileanRut.TryNormalize(input, out _, out _));
    }
}
