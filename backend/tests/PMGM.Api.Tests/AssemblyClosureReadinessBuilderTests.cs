using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyClosureReadinessBuilderTests
{
    private static AssemblyRecord Draft() => new() { Name = "Gran Asamblea 2026", MeetingDate = new(2026, 12, 5), CutoffDate = new(2026, 11, 30), CreatedBySubject = "ri" };

    private static AssemblyReadinessFacts Facts(int candidates = 0, string[]? inventory = null, int members = 0, string[]? statuses = null,
        int workshops = 0, int signed = 0, int restrictions = 0) =>
        new(candidates, inventory ?? [], candidates > 0 ? "venerable_master" : null, members, statuses ?? [], workshops, signed, restrictions);

    [Fact]
    public void Sin_datos_nada_esta_listo_y_el_cierre_sigue_bloqueado()
    {
        var r = AssemblyClosureReadinessBuilder.Build(Draft(), Facts(inventory: ["MASTER_OFFICE_RECORDS_MISSING"]));
        Assert.False(r.MayClose);
        Assert.True(r.Provisional);
        Assert.Equal("blocked", r.Items.Single(i => i.Code == "master_terms").State);
        Assert.Contains("Carga inicial", r.Items.Single(i => i.Code == "master_terms").Detail);
        Assert.Equal("pending", r.Items.Single(i => i.Code == "roster").State);
        Assert.Contains("closure", r.BlockingCodes);
    }

    [Fact]
    public void Con_todo_acreditado_el_cierre_aun_no_se_habilita_sin_cobertura_de_derechos()
    {
        var r = AssemblyClosureReadinessBuilder.Build(Draft(), Facts(candidates: 3, members: 3, statuses: ["habilitado", "inhabilitado", "solo_asistencia"], workshops: 3, signed: 3));
        Assert.Equal("ok", r.Items.Single(i => i.Code == "roster").State);
        Assert.Equal("ok", r.Items.Single(i => i.Code == "evaluation").State);
        Assert.Equal("ok", r.Items.Single(i => i.Code == "grand_treasury").State);
        Assert.Equal("pending", r.Items.Single(i => i.Code == "rights").State);
        Assert.False(r.MayClose);
    }

    [Fact]
    public void Cuenta_asambleistas_pendientes_y_talleres_sin_revision_firmada()
    {
        var r = AssemblyClosureReadinessBuilder.Build(Draft(), Facts(candidates: 5, members: 4, statuses: ["habilitado", "pendiente", "pendiente", "habilitado"], workshops: 4, signed: 1, restrictions: 2));
        Assert.Equal(2, r.Items.Single(i => i.Code == "evaluation").Count);
        Assert.Equal(3, r.Items.Single(i => i.Code == "grand_treasury").Count);
        Assert.Contains("2 restricciones", r.Items.Single(i => i.Code == "rights").Detail);
    }

    [Fact]
    public void Una_asamblea_cerrada_o_con_corte_posterior_a_la_reunion_queda_bloqueada()
    {
        var closed = Draft(); closed.Status = "closed";
        Assert.Equal("blocked", AssemblyClosureReadinessBuilder.Build(closed, Facts()).Items[0].State);
        var bad = Draft(); bad.CutoffDate = bad.MeetingDate.AddDays(1);
        Assert.Equal("blocked", AssemblyClosureReadinessBuilder.Build(bad, Facts()).Items[0].State);
    }
}
