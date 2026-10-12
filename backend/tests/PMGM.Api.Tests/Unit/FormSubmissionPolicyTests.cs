using System.Text.Json;
using PMGM.Api.Modules.FormConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Unit;

public sealed class FormSubmissionPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);

    private static FormSchemaSnapshot Schema(params FormFieldDefinition[] fields)
        => new("candidate_intake", 2, Start, fields);

    private static IReadOnlyDictionary<string, JsonElement> Values(
        params (string Key, object? Value)[] entries)
        => entries.ToDictionary(x => x.Key, x => JsonSerializer.SerializeToElement(x.Value),
            StringComparer.Ordinal);

    [Fact]
    public void ValidCaptureRespectsPublishedVersionAndTypedOptions()
    {
        var schema = Schema(
            new("name", "Nombre", "text", Required: true),
            new("choices", "Categorías", "multi_select", Options: ["A", "B"]),
            new("consent", "Consentimiento", "boolean", Required: true),
            new("count", "Cantidad", "number", Required: true));

        var result = FormSubmissionPolicy.Validate(schema, 2, Values(
            ("name", "María"), ("choices", new[] { "A", "B" }),
            ("consent", false), ("count", 0)));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void DifferentVersionCannotBeUsedEvenWithMatchingFieldKeys()
    {
        var result = FormSubmissionPolicy.Validate(
            Schema(new("name", "Nombre", "text")), 1, Values(("name", "Nombre")));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("versión"));
    }

    [Fact]
    public void UnknownHiddenDisabledOrReadonlyValuesAreRejected()
    {
        var schema = Schema(
            new("core_id", "Identificador", "reference", IsCore: true, Editable: false),
            new("hidden_note", "Secreto", "text", Visible: false),
            new("retired", "Anterior", "text", Enabled: false, Editable: false),
            new("active", "Activo", "text"));

        var result = FormSubmissionPolicy.Validate(schema, 2, Values(
            ("core_id", Guid.NewGuid().ToString()), ("hidden_note", "Privado"),
            ("retired", "Anterior"), ("unknown", "Valor"), ("active", "Permitido")));

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Errors.Count);
        Assert.DoesNotContain(result.Errors, x => x.Contains("Privado"));
    }

    [Fact]
    public void RequiredFieldsRejectEmptyButAcceptFalseAndZero()
    {
        var schema = Schema(
            new("name", "Nombre", "text", Required: true),
            new("yes_no", "Sí/No", "boolean", Required: true),
            new("amount", "Monto", "currency", Required: true));

        var result = FormSubmissionPolicy.Validate(schema, 2, Values(
            ("name", "   "), ("yes_no", false), ("amount", 0)));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("name", result.Errors[0]);
    }

    [Fact]
    public void SelectionValuesMustBelongToCatalogAndCannotRepeat()
    {
        var schema = Schema(
            new("one", "Elección", "single_select", Options: ["A", "B"]),
            new("many", "Varias", "multi_select", Options: ["A", "B"]));

        var result = FormSubmissionPolicy.Validate(schema, 2, Values(
            ("one", "C"), ("many", new[] { "A", "A" })));

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ConditionalFieldRequiresValueForItsParent()
    {
        var schema = Schema(
            new("has_other", "¿Otro?", "boolean"),
            new("other", "Detalle", "text", Required: true, DependsOnKey: "has_other"));

        var withoutParent = FormSubmissionPolicy.Validate(schema, 2,
            Values(("other", "Detalle")));
        var withParent = FormSubmissionPolicy.Validate(schema, 2,
            Values(("has_other", true), ("other", "Detalle")));
        var missingChild = FormSubmissionPolicy.Validate(schema, 2,
            Values(("has_other", true)));

        Assert.False(withoutParent.IsValid);
        Assert.True(withParent.IsValid);
        Assert.False(missingChild.IsValid);
    }

    [Fact]
    public void InvalidFormatsAndInlineBinaryOrUrlReferencesFailClosed()
    {
        var schema = Schema(
            new("birth_date", "Fecha", "date"),
            new("time", "Hora", "time"),
            new("email", "Correo", "email"),
            new("phone", "Teléfono", "phone"),
            new("photo", "Foto", "photo"));

        var bad = FormSubmissionPolicy.Validate(schema, 2, Values(
            ("birth_date", "11/10/2026"), ("time", "28:00"),
            ("email", "Persona <persona@example.org>"), ("phone", "abcdef"),
            ("photo", "https://example.org/private.jpg")));

        Assert.False(bad.IsValid);
        Assert.Equal(5, bad.Errors.Count);
    }

    [Fact]
    public void ReadonlyServerOwnedRequiredFieldIsNotClientWritable()
    {
        var schema = Schema(
            new("candidate_id", "Id", "reference", IsCore: true,
                Required: true, Editable: false),
            new("notes", "Observaciones", "multiline"));

        var emptyClientCapture = FormSubmissionPolicy.Validate(schema, 2, Values());
        var spoofedId = FormSubmissionPolicy.Validate(schema, 2,
            Values(("candidate_id", Guid.NewGuid().ToString())));

        Assert.True(emptyClientCapture.IsValid);
        Assert.False(spoofedId.IsValid);
    }

    [Fact]
    public void InvalidRuntimeValueShapeIsRejectedWithoutReturningPrivateContent()
    {
        var schema = Schema(new("name", "Nombre", "text"));
        var attempted = "secreto personal";

        var result = FormSubmissionPolicy.Validate(schema, 2,
            Values(("name", new { injected = attempted })));

        Assert.False(result.IsValid);
        Assert.DoesNotContain(result.Errors, x => x.Contains(attempted));
    }
}
