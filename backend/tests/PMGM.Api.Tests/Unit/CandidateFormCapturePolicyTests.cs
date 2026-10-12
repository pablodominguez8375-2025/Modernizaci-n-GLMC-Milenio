using System.Text.Json;
using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class CandidateFormCapturePolicyTests
{
    private static FormSchemaSnapshot Schema()
        => new(FormSchemaInstitutionalCatalog.CandidateProfile, 1,
            DateTimeOffset.UtcNow.AddHours(-1),
            FormSchemaInstitutionalCatalogTests.ValidFields());

    [Fact]
    public void AcceptsConfiguredValuesWithoutDuplicatingCoreFields()
    {
        var values = new Dictionary<string, JsonElement>
        {
            ["custom_note"] = JsonSerializer.SerializeToElement("Nota privada sintética.")
        };
        var result = CandidateFormCapturePolicy.Validate(Schema(), 1, values);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void RejectsUntrustedCoreFieldsAndUnknownKeys()
    {
        var values = new Dictionary<string, JsonElement>
        {
            ["first_names"] = JsonSerializer.SerializeToElement("Nombre alterado"),
            ["custom_note"] = JsonSerializer.SerializeToElement("nota")
        };
        Assert.False(CandidateFormCapturePolicy.Validate(Schema(), 1, values).IsValid);
        Assert.False(CandidateFormCapturePolicy.Validate(Schema(), 1,
            new Dictionary<string, JsonElement>
            {
                ["unpublished_extra"] = JsonSerializer.SerializeToElement("no autorizado")
            }).IsValid);
    }

    [Fact]
    public void RejectsWrongVersionAndOversizedPrivatePayload()
    {
        var longText = new string('n', CandidateFormCapturePolicy.MaxPayloadBytes + 1);
        var values = new Dictionary<string, JsonElement>
        {
            ["custom_note"] = JsonSerializer.SerializeToElement(longText)
        };
        Assert.False(CandidateFormCapturePolicy.Validate(Schema(), 2,
            new Dictionary<string, JsonElement>()).IsValid);
        Assert.False(CandidateFormCapturePolicy.Validate(Schema(), 1, values).IsValid);
    }

    [Fact]
    public void RefusesExternalReferencesUntilResourceAuthorizationExists()
    {
        var schema = Schema() with
        {
            Fields = Schema().Fields.Append(new FormFieldDefinition(
                "custom_document", "Respaldo", "document", Classification: FormFieldClassification.Restricted))
                .ToArray()
        };
        var values = new Dictionary<string, JsonElement>
        {
            ["custom_document"] = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D"))
        };
        Assert.False(CandidateFormCapturePolicy.Validate(schema, 1, values).IsValid);
    }

    [Fact]
    public void RejectsRequiredConfiguredFieldWhenMissing()
    {
        var schema = Schema() with
        {
            Fields = Schema().Fields.Select(x => x.Key == "custom_note"
                ? x with { Required = true } : x).ToArray()
        };
        Assert.False(CandidateFormCapturePolicy.Validate(schema, 1,
            new Dictionary<string, JsonElement>()).IsValid);
    }
}
