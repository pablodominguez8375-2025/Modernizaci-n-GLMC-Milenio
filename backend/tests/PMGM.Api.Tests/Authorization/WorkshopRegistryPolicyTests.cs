using System.Text;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>#1035: reglas del registro de Talleres (decisión del PO 09-11-2026).</summary>
public sealed class WorkshopRegistryPolicyTests
{
    private static WorkshopRegistryDecisionCode D(string? action, bool isMaster, WorkshopRegistryFacts facts, string? reason, string? confirmation, bool retireEnabled = true)
        => WorkshopRegistryPolicy.Decide(action, isMaster, facts, reason, confirmation, retireEnabled);

    private static readonly Guid Id = Guid.Parse("7c3e51b0-2300-4000-8000-000000000230");
    private const string Reason = "Motivo suficiente";
    private static WorkshopRegistryFacts Facts(string type = "workshop", string status = "active", bool hasData = false, string? number = "230")
        => new(Id, number, type, status, hasData);

    [Fact]
    public void Master_deletes_empty_active_workshop_with_number_confirmation()
        => Assert.Equal(WorkshopRegistryDecisionCode.Allowed, D("delete", true, Facts(), Reason, "230"));

    [Fact]
    public void Delete_is_refused_when_workshop_has_data()
        => Assert.Equal(WorkshopRegistryDecisionCode.HasData, D("delete", true, Facts(hasData: true), Reason, "230"));

    [Fact]
    public void Retire_is_allowed_with_data_and_refused_when_already_retired()
    {
        Assert.Equal(WorkshopRegistryDecisionCode.Allowed, D("retire", true, Facts(hasData: true), Reason, "230"));
        Assert.Equal(WorkshopRegistryDecisionCode.AlreadyRetired, D("retire", true, Facts(status: "retired"), Reason, "230"));
    }

    [Fact]
    public void Reactivate_needs_reason_but_not_confirmation_and_only_for_retired()
    {
        Assert.Equal(WorkshopRegistryDecisionCode.Allowed, D("reactivate", true, Facts(status: "retired"), Reason, null));
        Assert.Equal(WorkshopRegistryDecisionCode.NotRetired, D("reactivate", true, Facts(), Reason, null));
        Assert.Equal(WorkshopRegistryDecisionCode.InvalidReason, D("reactivate", true, Facts(status: "retired"), "corto", null));
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("retire")]
    [InlineData("reactivate")]
    public void Only_master_mutates(string action)
        => Assert.Equal(WorkshopRegistryDecisionCode.NotMaster, D(action, false, Facts(status: action == "reactivate" ? "retired" : "active"), Reason, "230"));

    [Theory]
    [InlineData("grand_lodge")]
    [InlineData("support")]
    [InlineData("SUPPORT")]
    public void Grand_lodge_and_support_workshop_are_protected(string type)
    {
        foreach (var action in new[] { "delete", "retire", "reactivate" })
            Assert.Equal(WorkshopRegistryDecisionCode.Protected, D(action, true, Facts(type: type, number: "00"), Reason, "00"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("         ")]
    [InlineData("corto")]
    public void Reason_must_have_ten_trimmed_characters(string? reason)
        => Assert.Equal(WorkshopRegistryDecisionCode.InvalidReason, D("retire", true, Facts(), reason, "230"));

    [Fact]
    public void Reason_has_a_maximum_length()
    {
        Assert.Equal(WorkshopRegistryDecisionCode.Allowed, D("retire", true, Facts(), new string('a', 500), "230"));
        Assert.Equal(WorkshopRegistryDecisionCode.InvalidReason, D("retire", true, Facts(), new string('a', 501), "230"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("23")]
    [InlineData("2300")]
    public void Delete_and_retire_require_exact_confirmation(string? confirmation)
    {
        Assert.Equal(WorkshopRegistryDecisionCode.ConfirmationMismatch, D("delete", true, Facts(), Reason, confirmation));
        Assert.Equal(WorkshopRegistryDecisionCode.ConfirmationMismatch, D("retire", true, Facts(), Reason, confirmation));
    }

    [Fact]
    public void Workshop_without_number_is_confirmed_with_its_short_code()
    {
        var facts = Facts(number: null);
        Assert.Equal("7C3E51B0", facts.ConfirmationText);
        Assert.Equal(WorkshopRegistryDecisionCode.Allowed, D("retire", true, facts, Reason, "7C3E51B0"));
        Assert.Equal(WorkshopRegistryDecisionCode.ConfirmationMismatch, D("retire", true, facts, Reason, "7c3e51b0"));
    }

    [Fact]
    public void Retire_is_explicitly_blocked_until_cross_module_enforcement_exists()
    {
        Assert.Equal(WorkshopRegistryDecisionCode.RetireNotEnabled, D("retire", true, Facts(hasData: true), Reason, "230", retireEnabled: false));
        // el bloqueo no afecta a las demás acciones
        Assert.Equal(WorkshopRegistryDecisionCode.Allowed, D("delete", true, Facts(), Reason, "230", retireEnabled: false));
        Assert.Equal(WorkshopRegistryDecisionCode.Allowed, D("reactivate", true, Facts(status: "retired"), Reason, null, retireEnabled: false));
    }

    [Theory]
    [InlineData("workshop")]
    [InlineData("lodge")]
    [InlineData(null)]
    public void Any_organization_numbered_00_is_protected_whatever_its_type(string? type)
    {
        foreach (var action in new[] { "delete", "retire", "reactivate" })
            Assert.Equal(WorkshopRegistryDecisionCode.Protected, D(action, true, Facts(type: type!, number: "00"), Reason, "00"));
        Assert.Equal(WorkshopRegistryDecisionCode.Protected, D("retire", true, Facts(type: type!, number: " 00 "), Reason, "00"));
    }

    [Fact]
    public void Support_workshop_is_protected_by_its_fixed_id_even_if_type_and_number_are_changed()
    {
        var facts = new WorkshopRegistryFacts(WorkshopRegistryFacts.SupportWorkshopId, "7", "workshop", "active", false);
        Assert.True(facts.Protected);
        Assert.Equal(WorkshopRegistryDecisionCode.Protected, D("delete", true, facts, Reason, "7"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("100")]
    public void Numbers_that_only_resemble_00_are_not_protected(string number)
        => Assert.False(Facts(number: number).Protected);

    [Theory]
    [InlineData("other", "grand_lodge", "bootstrap")]
    [InlineData("other", "support", "bootstrap")]
    [InlineData("other", "workshop", "other")]
    [InlineData("qa_seed", "grand_lodge", "qa_seed")]
    [InlineData("initial_load", "workshop", "initial_load")]
    public void Display_origin_derives_technical_organizations_without_a_migration_update(string origin, string type, string expected)
        => Assert.Equal(expected, WorkshopRegistryCodes.DisplayOrigin(origin, type));

    [Fact]
    public void Unknown_action_is_rejected_before_anything_else()
        => Assert.Equal(WorkshopRegistryDecisionCode.UnknownAction, D("purge", true, Facts(), Reason, "230"));

    [Fact]
    public void Retired_workshop_cannot_be_deleted()
        => Assert.Equal(WorkshopRegistryDecisionCode.NotActive, D("delete", true, Facts(status: "retired"), Reason, "230"));

    [Theory]
    [InlineData("initial_load", "initial_load")]
    [InlineData("qa_seed", "qa_seed")]
    [InlineData("bootstrap", "bootstrap")]
    [InlineData("raro", "other")]
    [InlineData(null, "other")]
    public void Origin_is_normalized(string? origin, string expected)
        => Assert.Equal(expected, WorkshopRegistryCodes.NormalizeOrigin(origin));

    private static string Csv(params WorkshopCsvRow[] rows) => Encoding.UTF8.GetString(WorkshopRegistryCsv.Build(rows));

    [Fact]
    public void Csv_has_bom_semicolons_crlf_and_the_demo_header()
    {
        var bytes = WorkshopRegistryCsv.Build([]);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, 0x43 }, bytes[..4]); // un solo BOM
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Equal("﻿Código interno;Número;Nombre;Tipo;Oriente;Ciudad;Origen;Estado;Tiene datos asociados;Creado (UTC);Identificador\r\n", text);
    }

    [Fact]
    public void Csv_row_maps_labels_and_protects_formulas_and_quotes()
    {
        var created = new DateTimeOffset(2026, 11, 8, 22, 14, 0, TimeSpan.Zero);
        var text = Csv(
            new WorkshopCsvRow(Id, "7C3E51B0", "230", "=HYPERLINK(\"x\")", "workshop", "SCL", "Santiago; Centro", "initial_load", "retired", true, created),
            new WorkshopCsvRow(Guid.Empty, "00000000", null, "Gran Logia", "grand_lodge", null, null, "otra", "active", false, created));
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal("7C3E51B0;230;\"'=HYPERLINK(\"\"x\"\")\";Taller;SCL;\"Santiago; Centro\";Carga inicial;De baja;Sí;2026-11-08T22:14:00Z;7c3e51b0-2300-4000-8000-000000000230", lines[1]);
        Assert.Equal("00000000;;Gran Logia;Gran Logia;;;Otro;Activo;No;2026-11-08T22:14:00Z;00000000-0000-0000-0000-000000000000", lines[2]);
    }

    [Theory]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@x", "'@x")]
    [InlineData("normal", "normal")]
    [InlineData(null, "")]
    public void Csv_cell_guards_formula_prefixes(string? input, string expected)
        => Assert.Equal(expected, WorkshopRegistryCsv.Cell(input));
}
