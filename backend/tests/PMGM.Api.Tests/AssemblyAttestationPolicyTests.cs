using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyAttestationPolicyTests
{
    private static AssemblyAttestationEventRecord Ev(string kind, string action, Guid? reverses = null, int minutes = 0) =>
        new() { AssemblyId = Guid.NewGuid(), Kind = kind, Action = action, ActaReference = "Acta 12", Statement = "Declaración de prueba.", RecordedBySubject = "ri", ReversesEventId = reverses };

    [Fact]
    public void Exige_tipo_accion_acta_y_declaracion()
    {
        Assert.Null(AssemblyAttestationPolicy.Validate("membership", "attest", "Acta 12", "Declaración suficiente."));
        Assert.Equal("INVALID_ATTESTATION_KIND", AssemblyAttestationPolicy.Validate("otro", "attest", "Acta", "Declaración suficiente."));
        Assert.Equal("INVALID_ATTESTATION_ACTION", AssemblyAttestationPolicy.Validate("membership", "delete", "Acta", "Declaración suficiente."));
        Assert.Equal("ACTA_REFERENCE_REQUIRED", AssemblyAttestationPolicy.Validate("membership", "attest", " ", "Declaración suficiente."));
        Assert.Equal("STATEMENT_REQUIRED", AssemblyAttestationPolicy.Validate("membership", "attest", "Acta", "corta"));
    }

    [Fact]
    public void Una_constancia_revocada_deja_de_estar_vigente()
    {
        var a = Ev("master_periods", "attest");
        var r = Ev("master_periods", "revoke", a.Id);
        Assert.Contains("master_periods", AssemblyAttestationPolicy.Current([a]).Keys);
        Assert.Empty(AssemblyAttestationPolicy.Current([a, r]));
    }

    [Fact]
    public void Tras_revocar_se_puede_certificar_de_nuevo_y_cada_tipo_es_independiente()
    {
        var a = Ev("honor_coverage", "attest");
        var r = Ev("honor_coverage", "revoke", a.Id);
        var b = Ev("honor_coverage", "attest");
        var m = Ev("membership", "attest");
        var cur = AssemblyAttestationPolicy.Current([a, r, b, m]);
        Assert.Equal(b.Id, cur["honor_coverage"].Id);
        Assert.True(cur.ContainsKey("membership"));
        Assert.False(cur.ContainsKey("regimen_coverage"));
    }

    [Fact]
    public void El_informe_marca_listo_solo_lo_certificado_y_nunca_habilita_el_cierre()
    {
        var asm = new AssemblyRecord { Name = "GA", MeetingDate = new(2026, 12, 5), CutoffDate = new(2026, 11, 30), CreatedBySubject = "ri" };
        AssemblyReadinessFacts F(params string[] kinds) => new(3, [], "venerable_master", 3, ["habilitado", "habilitado", "inhabilitado"], 3, 3, 0, kinds);
        var none = AssemblyClosureReadinessBuilder.Build(asm, F());
        Assert.Equal("pending", none.Items.Single(i => i.Code == "master_terms").State);
        Assert.Equal("pending", none.Items.Single(i => i.Code == "membership").State);
        Assert.Equal("pending", none.Items.Single(i => i.Code == "rights").State);

        var onlyHonor = AssemblyClosureReadinessBuilder.Build(asm, F("honor_coverage"));
        Assert.Equal("pending", onlyHonor.Items.Single(i => i.Code == "rights").State);
        Assert.Contains("Régimen Interior", onlyHonor.Items.Single(i => i.Code == "rights").Detail);

        var all = AssemblyClosureReadinessBuilder.Build(asm, F("master_periods", "membership", "honor_coverage", "regimen_coverage"));
        Assert.All(all.Items.Where(i => i.Code != "closure"), i => Assert.Equal("ok", i.State));
        // Con todo certificado el cierre pasa a «puede intentarse»; el servidor igual vuelve a verificar al cerrar.
        Assert.True(all.MayClose);
        Assert.Equal("pending", all.Items.Single(i => i.Code == "closure").State);
        Assert.Empty(all.BlockingCodes);
        Assert.False(AssemblyClosureReadinessBuilder.Build(asm, F("membership")).MayClose);

        asm.Status = "closed"; asm.ClosedAtUtc = DateTimeOffset.UtcNow; asm.ClosedBySubject = "ri"; asm.ClosureEvidenceDigest = new string('a', 64);
        var done = AssemblyClosureReadinessBuilder.Build(asm, F());
        Assert.False(done.MayClose);
        Assert.False(done.Provisional);
        Assert.Equal("ok", done.Items.Single(i => i.Code == "closure").State);
        Assert.Equal(new string('a', 64), done.ClosureDigest);
    }
}
