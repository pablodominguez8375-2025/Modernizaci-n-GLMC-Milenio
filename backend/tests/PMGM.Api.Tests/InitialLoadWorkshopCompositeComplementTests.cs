using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

/// <summary>
/// Pruebas COMPLEMENTARIAS de la política compuesta (Number, OrienteCode) de #1049.
/// No duplican InitialLoadWorkshopCompositeKeyPolicyTests. Fijan: el lector real entrega
/// Code vacío (sin mapeo workshopCode en Organization), la normalización solo detecta
/// colisiones (nunca declara identidad canónica) y todo conflicto bloquea el lote completo.
/// Sin Apply, sin datos reales.
/// </summary>
public sealed class InitialLoadWorkshopCompositeComplementTests
{
    private static InitialLoadWorkshopMatchPolicy.Incoming In(
        string code, string number, string oriente, string name = "Taller") =>
        new(code, number, name, oriente, "Santiago", "CL");

    // El lector EF actual representa el código persistido como cadena vacía.
    private static InitialLoadWorkshopMatchPolicy.Existing Db(
        string number, string oriente, string name = "Taller") =>
        new("", number, name, oriente, "Santiago", "CL");

    [Fact]
    public void Persisted_rows_with_empty_codes_are_not_duplicates_when_identities_differ()
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In("A", "5", "SCL")], [Db("23", "SCL"), Db("23", "VAL"), Db("7", "SCL")]);
        Assert.True(result.Valid);
        Assert.Equal(1, result.NewRows);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Legacy_code_policy_would_reject_the_real_reader_snapshot_so_it_must_not_be_used()
    {
        var legacyIncoming = new[] { new InitialLoadWorkshopMatchPolicy.Incoming("A", "5", "Taller", "SCL", "Santiago", "CL") };
        var result = InitialLoadWorkshopBatchPolicy.Inspect(legacyIncoming, [Db("23", "SCL")]);
        Assert.False(result.Valid);
        Assert.Equal("SNAPSHOT_KEYS_AMBIGUOUS", result.Code);
    }

    [Fact]
    public void Same_csv_code_on_two_rows_with_different_identities_blocks_the_batch()
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In("A", "5", "SCL"), In("A", "6", "SCL")], [Db("23", "SCL")]);
        Assert.False(result.Valid);
        Assert.Equal("BATCH_KEYS_INVALID_OR_DUPLICATE", result.Code);
        Assert.Equal(0, result.NewRows);
    }

    [Theory]
    [InlineData("23", "\tSCL")]
    [InlineData("23", "SCL ")]
    [InlineData("23\n", "SCL")]
    public void Surrounding_whitespace_variants_never_look_absent(string number, string oriente)
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In("A", number, oriente)], [Db("23", "SCL")]);
        Assert.False(result.Valid);
        Assert.Equal(0, result.NewRows);
        Assert.Equal(0, result.ExactSkips);
        Assert.Equal(1, result.BlockedRows);
    }

    [Fact]
    public void Zero_padded_zero_variants_in_the_snapshot_are_ambiguous()
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In("A", "5", "SCL")], [Db("0", "SCL"), Db("00", "SCL")]);
        Assert.False(result.Valid);
        Assert.Equal("SNAPSHOT_KEYS_AMBIGUOUS", result.Code);
    }

    [Fact]
    public void One_contradictory_row_blocks_the_whole_batch_without_partial_counts()
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In("A", "23", "SCL", "Otro nombre"), In("B", "7", "SCL"), In("C", "9", "SCL")],
            [Db("23", "SCL", "Nombre original")]);
        Assert.False(result.Valid);
        Assert.Equal("BATCH_NATURAL_KEY_CONFLICT", result.Code);
        Assert.Equal(0, result.NewRows);
        Assert.Equal(0, result.ExactSkips);
        Assert.Equal(1, result.BlockedRows);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Exact_same_form_match_is_a_skip_and_never_a_write()
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In("LOCAL-1", "23", "SCL")], [Db("23", "SCL")]);
        Assert.True(result.Valid);
        Assert.Equal(1, result.ExactSkips);
        Assert.Equal(0, result.NewRows);
        Assert.Equal(0, result.WritesPerformed);
    }
}
