using System.Text.Json;
using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class FormSchemaVersionTests
{
    private static FormSchemaSnapshot Snapshot(int version = 1) => new(
        "candidate_profile", version,
        new DateTimeOffset(2026, 10, version, 0, 0, 0, TimeSpan.Zero),
        [new FormFieldDefinition("candidate_id", "Identidad institucional", "reference",
            IsCore: true, Required: true, Classification: FormFieldClassification.Sensitive),
         new FormFieldDefinition("custom_note", "Anotación", "text")]);

    private static FormSchemaVersion Row(FormSchemaSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot);
        return new FormSchemaVersion
        {
            FormCode = snapshot.FormCode,
            ScopeKey = FormSchemaScope.Order,
            Version = snapshot.Version,
            EffectiveFromUtc = snapshot.EffectiveFromUtc,
            SchemaJson = json,
            PayloadSha256 = FormSchemaVersionStore.Fingerprint(json),
            RecordedBySubject = "admin-ci"
        };
    }

    [Fact]
    public void ScopeIsStableAndNeverUsesEmptyLodgeId()
    {
        var lodge = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Assert.Equal("order", FormSchemaScope.ForOrganization(null));
        Assert.Equal("lodge:00000000000000000000000000000001",
            FormSchemaScope.ForOrganization(lodge));
        Assert.Throws<ArgumentException>(() => FormSchemaScope.ForOrganization(Guid.Empty));
    }

    [Fact]
    public void HistoricalSnapshotRequiresMatchingPayloadChecksumAndVersion()
    {
        var snapshot = Snapshot();
        var row = Row(snapshot);
        var roundTrip = FormSchemaVersionStore.Decode(row);
        Assert.Equal(snapshot.FormCode, roundTrip.FormCode);
        Assert.Equal(snapshot.Version, roundTrip.Version);
        Assert.Equal(snapshot.Fields.Count, roundTrip.Fields.Count);

        Assert.Throws<InvalidDataException>(() => FormSchemaVersionStore.Decode(
            WithInvalidChecksum(Row(snapshot))));
    }

    [Fact]
    public void MetadataMismatchFailsClosedEvenWhenChecksumIsCorrect()
    {
        var row = Row(Snapshot(1));
        var other = new FormSchemaVersion
        {
            FormCode = row.FormCode,
            ScopeKey = row.ScopeKey,
            Version = 2,
            EffectiveFromUtc = row.EffectiveFromUtc,
            SchemaJson = row.SchemaJson,
            PayloadSha256 = row.PayloadSha256,
            RecordedBySubject = row.RecordedBySubject
        };
        Assert.Throws<InvalidDataException>(() => FormSchemaVersionStore.Decode(other));
    }

    private static FormSchemaVersion WithInvalidChecksum(FormSchemaVersion row) => new()
    {
        FormCode = row.FormCode,
        ScopeKey = row.ScopeKey,
        Version = row.Version,
        EffectiveFromUtc = row.EffectiveFromUtc,
        SchemaJson = row.SchemaJson,
        PayloadSha256 = new string('0', 64),
        RecordedBySubject = row.RecordedBySubject
    };
}
