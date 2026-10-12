using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class FormSchemaInstitutionalCatalogTests
{
    internal static FormFieldDefinition[] ValidFields() =>
    [
        new("candidate_id", "Expediente", "reference",
            IsCore: true, Required: true, Editable: false,
            Classification: FormFieldClassification.Sensitive),
        new("organization_id", "Taller", "reference",
            IsCore: true, Required: true, Editable: false,
            Classification: FormFieldClassification.Internal),
        new("first_names", "Nombres", "text",
            IsCore: true, Required: true,
            Classification: FormFieldClassification.Sensitive),
        new("paternal_surname", "Apellido paterno", "text",
            IsCore: true, Required: true,
            Classification: FormFieldClassification.Sensitive),
        new("maternal_surname", "Apellido materno", "text",
            IsCore: true, Classification: FormFieldClassification.Sensitive),
        new("rut_or_institutional_id", "Identificador", "text",
            IsCore: true, Required: true,
            Classification: FormFieldClassification.Restricted),
        new("process_status", "Estado", "text",
            IsCore: true, Required: true, Editable: false,
            Classification: FormFieldClassification.Internal),
        new("custom_note", "Observación", "multiline",
            Classification: FormFieldClassification.Restricted)
    ];

    private static FormSchemaSnapshot Schema(params FormFieldDefinition[] fields)
        => new(FormSchemaInstitutionalCatalog.CandidateProfile, 1,
            DateTimeOffset.UtcNow.AddMinutes(3), fields);

    [Fact]
    public void TrustedCandidateCoreAcceptsProtectedFields()
    {
        var schema = Schema(ValidFields());
        var result = FormSchemaInstitutionalCatalog.ValidateForPublication(schema);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.True(FormSchemaPolicy.ValidateNext(null, schema,
            FormSchemaInstitutionalCatalog.TrustedCoreKeys(schema.FormCode)).IsValid);
    }

    [Fact]
    public void RejectsCoreDemotionTypeChangeAndClientOwnedInstitutionalIdentifiers()
    {
        var original = ValidFields();
        var altered = original.Select(x => x.Key == "candidate_id"
            ? x with { IsCore = false, TypeCode = "text", Editable = true }
            : x).ToArray();
        var result = FormSchemaInstitutionalCatalog.ValidateForPublication(Schema(altered));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("candidate_id", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsPublicCandidateFieldsAndInsufficientDocumentPrivacy()
    {
        var baseFields = ValidFields();
        var publicField = baseFields.Concat(
            [new FormFieldDefinition("photo", "Fotografía", "photo",
                Classification: FormFieldClassification.Public)]).ToArray();
        var result = FormSchemaInstitutionalCatalog.ValidateForPublication(Schema(publicField));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("photo", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsUnapprovedFormCodeEvenWithValidFields()
    {
        var schema = Schema(ValidFields()) with { FormCode = "unreviewed_private_form" };
        Assert.False(FormSchemaInstitutionalCatalog.ValidateForPublication(schema).IsValid);
        Assert.False(FormSchemaInstitutionalCatalog.Supports(schema.FormCode));
    }

    [Fact]
    public void RejectsMissingMandatoryIdentityField()
    {
        var result = FormSchemaInstitutionalCatalog.ValidateForPublication(
            Schema(ValidFields().Where(x => x.Key != "rut_or_institutional_id").ToArray()));
        Assert.False(result.IsValid);
    }
}
