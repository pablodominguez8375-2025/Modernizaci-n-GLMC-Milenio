using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

/// <summary>
/// Caracterización de la identidad de Talleres del plan de Carga inicial (#694 / #1049).
/// Diseño candidato (ADR 2026-11-20): workshopCode es solo clave local del lote CSV;
/// la identidad en BD es (Number, OrienteCode) y Number solo NO es clave segura.
/// Estas pruebas fijan lo que la política hace HOY; no autorizan Apply ni datos reales.
/// </summary>
public sealed class InitialLoadWorkshopIdentityTests
{
    private static InitialLoadWorkshopMatchPolicy.Incoming In(
        string code, string number = "23", string oriente = "SCL") =>
        new(code, number, "Taller", oriente, "Santiago", "CL");

    private static InitialLoadWorkshopMatchPolicy.Existing Db(
        string code, string number = "23", string oriente = "SCL") =>
        new(code, number, "Taller", oriente, "Santiago", "CL");

    [Fact]
    public void Same_number_in_a_different_oriente_is_not_a_collision_by_itself()
    {
        var result = InitialLoadWorkshopBatchPolicy.Inspect(
            [In("A", "23", "SCL")], [Db("B", "23", "VAL")]);
        Assert.True(result.Valid);
        Assert.Equal(1, result.NewRows);
        Assert.Equal(0, result.BlockedRows);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Same_code_and_number_but_different_oriente_is_contradictory_and_blocks_the_batch()
    {
        var result = InitialLoadWorkshopBatchPolicy.Inspect(
            [In("A", "23", "SCL"), In("B", "7", "SCL")], [Db("A", "23", "VAL")]);
        Assert.False(result.Valid);
        Assert.Equal("BATCH_NATURAL_KEY_CONFLICT", result.Code);
        Assert.Equal(0, result.NewRows);
        Assert.Equal(0, result.ExactSkips);
        Assert.Equal(1, result.BlockedRows);
    }

    [Theory]
    [InlineData("", "SCL")]
    [InlineData("SCL", "")]
    public void Empty_oriente_on_either_side_of_a_matched_row_never_counts_as_exact(
        string persisted, string incoming)
    {
        var match = InitialLoadWorkshopMatchPolicy.Inspect(
            In("A", "23", incoming), [Db("A", "23", persisted)]);
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Contradictory, match);
    }

    [Fact]
    public void Identical_number_and_oriente_under_the_same_code_is_exact()
    {
        var result = InitialLoadWorkshopBatchPolicy.Inspect(
            [In("A", "23", "SCL")], [Db("A", "23", "SCL")]);
        Assert.True(result.Valid);
        Assert.Equal(1, result.ExactSkips);
        Assert.Equal(0, result.NewRows);
    }

    [Fact]
    public void Empty_batch_is_valid_only_by_vacuity_and_never_applies()
    {
        var result = InitialLoadWorkshopBatchPolicy.Inspect([], [Db("A")]);
        Assert.True(result.Valid);
        Assert.Equal(0, result.NewRows);
        Assert.Equal(0, result.ExactSkips);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Missing_snapshot_or_batch_denies_without_counts()
    {
        Assert.Equal("SNAPSHOT_UNAVAILABLE", InitialLoadWorkshopBatchPolicy.Inspect(null, [Db("A")]).Code);
        Assert.Equal("SNAPSHOT_UNAVAILABLE", InitialLoadWorkshopBatchPolicy.Inspect([In("A")], null).Code);
    }
}
