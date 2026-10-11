using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class FormSchemaPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] CoreKeys = ["member_id"];

    private static FormSchemaSnapshot Schema(int version = 1, params FormFieldDefinition[] fields)
        => new("member_profile", version, Start.AddDays(version - 1),
            fields.Length == 0 ? [Core()] : fields);

    private static FormFieldDefinition Core() => new(
        "member_id", "Identificador institucional", "reference",
        IsCore: true, Classification: FormFieldClassification.Sensitive, Required: true);

    private static FormFieldDefinition Optional() => new(
        "contact_phone", "Teléfono", "phone", Classification: FormFieldClassification.Internal,
        SortOrder: 2);

    [Fact]
    public void ValidFirstVersionSupportsTypedOptionalFieldsWithoutChangingCore()
    {
        var next = Schema(1, Core(), Optional(),
            new("custom_tags", "Categorías", "multi_select",
                Options: ["A", "B"], Classification: FormFieldClassification.Internal));

        var result = FormSchemaPolicy.ValidateNext(null, next, CoreKeys);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void ValidSequentialVersionKeepsDisabledHistoricFieldWithItsType()
    {
        var initial = Schema(1, Core(), Optional());
        var next = Schema(2, Core(), Optional() with
            { Enabled = false, Visible = false, Editable = false, Required = false });

        var result = FormSchemaPolicy.ValidateNext(initial, next, CoreKeys);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void PublicationRequiresSequentialVersion(int nextVersion)
    {
        var result = FormSchemaPolicy.ValidateNext(Schema(), Schema(nextVersion), CoreKeys);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("consecutiva"));
    }

    [Fact]
    public void PublishedFormCodeCannotBeChanged()
    {
        var next = Schema(2) with { FormCode = "another_profile" };

        Assert.False(FormSchemaPolicy.ValidateNext(Schema(), next, CoreKeys).IsValid);
    }

    [Fact]
    public void MissingOrHiddenCoreFieldIsNeverAccepted()
    {
        var old = Schema(1, Core(), Optional());
        var missing = Schema(2, Optional());
        var hidden = Schema(2, Core() with { Visible = false }, Optional());

        Assert.False(FormSchemaPolicy.ValidateNext(old, missing, CoreKeys).IsValid);
        Assert.False(FormSchemaPolicy.ValidateNext(old, hidden, CoreKeys).IsValid);
    }

    [Fact]
    public void CoreIdentityCannotBeDemotedOrMadeOptional()
    {
        var old = Schema(1, Core());
        var demoted = Schema(2, Core() with { IsCore = false });
        var optional = Schema(2, Core() with { Required = false });

        Assert.False(FormSchemaPolicy.ValidateNext(old, demoted, CoreKeys).IsValid);
        Assert.False(FormSchemaPolicy.ValidateNext(old, optional, CoreKeys).IsValid);
    }

    [Fact]
    public void PrivacyClassificationCannotBeDowngradedAcrossVersions()
    {
        var original = Schema(1, Core(), Optional() with
            { Classification = FormFieldClassification.Sensitive });
        var changed = Schema(2, Core(), Optional() with
            { Classification = FormFieldClassification.Public });

        Assert.Contains(FormSchemaPolicy.ValidateNext(original, changed, CoreKeys).Errors,
            x => x.Contains("clasificación"));
    }

    [Fact]
    public void HistoricalFieldCannotBeDeletedOrRetypedSilently()
    {
        var old = Schema(1, Core(), Optional());
        var removed = Schema(2, Core());
        var changedType = Schema(2, Core(), Optional() with { TypeCode = "text" });

        Assert.False(FormSchemaPolicy.ValidateNext(old, removed, CoreKeys).IsValid);
        Assert.False(FormSchemaPolicy.ValidateNext(old, changedType, CoreKeys).IsValid);
    }

    [Fact]
    public void NewUserProvidedFieldCannotClaimInstitutionalCoreStatus()
    {
        var initial = Schema(1, Core(), Optional() with { IsCore = true });

        Assert.False(FormSchemaPolicy.ValidateNext(null, initial, CoreKeys).IsValid);
    }

    [Fact]
    public void DuplicateKeysOrInvalidTypesFailBeforePublication()
    {
        var duplicate = Schema(1, Core(), Optional(), Optional());
        var unknown = Schema(1, Core(), new("custom", "Mal definido", "executable"));

        Assert.False(FormSchemaPolicy.ValidateNext(null, duplicate, CoreKeys).IsValid);
        Assert.False(FormSchemaPolicy.ValidateNext(null, unknown, CoreKeys).IsValid);
    }

    [Fact]
    public void SelectionOptionsMustBeUniqueAndNonempty()
    {
        var empty = Schema(1, Core(), new("choice", "Opción", "single_select"));
        var duplicates = Schema(1, Core(), new("choice", "Opción", "multi_select",
            Options: ["A", "A"]));

        Assert.False(FormSchemaPolicy.ValidateNext(null, empty, CoreKeys).IsValid);
        Assert.False(FormSchemaPolicy.ValidateNext(null, duplicates, CoreKeys).IsValid);
    }

    [Fact]
    public void CircularOrMissingDependenciesAreRejected()
    {
        var circular = Schema(1, Core(),
            new("field_a", "A", "text", DependsOnKey: "field_b"),
            new("field_b", "B", "text", DependsOnKey: "field_a"));
        var missing = Schema(1, Core(),
            new("field_a", "A", "text", DependsOnKey: "missing_field"));

        Assert.False(FormSchemaPolicy.ValidateNext(null, circular, CoreKeys).IsValid);
        Assert.False(FormSchemaPolicy.ValidateNext(null, missing, CoreKeys).IsValid);
    }

    [Fact]
    public void FieldsMustNotBeRequiredWhenInvisibleOrEditableWhenDisabled()
    {
        var hiddenRequired = Schema(1, Core(), Optional() with { Visible = false, Required = true });
        var disabledEditable = Schema(1, Core(), Optional() with { Enabled = false, Editable = true });

        Assert.False(FormSchemaPolicy.ValidateNext(null, hiddenRequired, CoreKeys).IsValid);
        Assert.False(FormSchemaPolicy.ValidateNext(null, disabledEditable, CoreKeys).IsValid);
    }

    [Fact]
    public void EffectiveDateMustAdvanceAndCannotReuseOldTime()
    {
        var original = Schema();
        var next = Schema(2) with { EffectiveFromUtc = original.EffectiveFromUtc };

        Assert.False(FormSchemaPolicy.ValidateNext(original, next, CoreKeys).IsValid);
    }
}
