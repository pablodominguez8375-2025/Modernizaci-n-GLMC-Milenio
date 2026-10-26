using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyClosureEngineTests
{
    private static readonly DateOnly Cutoff = new(2026, 10, 19);
    private static AssemblyRecord Asm() => new() { Name = "GA", MeetingDate = new(2026, 11, 19), CutoffDate = Cutoff, CreatedBySubject = "ri" };
    private static AssemblyMemberStatusRecord Member(Guid org) => new()
    {
        AssemblyId = Guid.NewGuid(), PersonId = Guid.NewGuid(), MemberId = Guid.NewGuid(), OrganizationId = org,
        Category = "representative", SourceOfficeAssignmentId = Guid.NewGuid(), SourceEvidenceReference = "VM",
        FirstNamesAtCutoff = "A", LastNamesAtCutoff = "B", RutAtCutoff = "1-9", WorkshopAtCutoff = "T", VerifiedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
    };
    private static Dictionary<string, AssemblyAttestationEventRecord> AllAttestations() =>
        AssemblyClosureEngine.RequiredAttestations.ToDictionary(k => k, k => new AssemblyAttestationEventRecord
        { Kind = k, Action = "attest", ActaReference = "ACTA-" + k, Statement = "Declaración de prueba.", RecordedBySubject = "ri" });

    private static AssemblyClosureEngine.Evidence Ev(AssemblyMemberStatusRecord m, decimal? balance, bool active = true,
        Dictionary<string, AssemblyAttestationEventRecord>? att = null, IReadOnlyCollection<AssemblyDebtDecisionEventRecord>? debt = null,
        IReadOnlyCollection<InstitutionalRestrictionRecord>? restrictions = null) => new(
        att ?? AllAttestations(),
        balance is null ? new Dictionary<Guid, AssemblyWorkshopBalanceEventRecord>() : new Dictionary<Guid, AssemblyWorkshopBalanceEventRecord>
        { [m.OrganizationId] = new() { OrganizationId = m.OrganizationId, Source = AssemblyClosureEngine.SourceTreasury, AmountPending = balance.Value, Currency = "CLP", SourceReference = "OFICIO-1", RecordedBySubject = "ri" } },
        restrictions ?? [], debt ?? [], new Dictionary<Guid, bool> { [m.MemberId] = active });

    [Fact]
    public void Saldo_cero_y_derechos_certificados_habilitan_y_cualquier_saldo_positivo_bloquea()
    {
        var m = Member(Guid.NewGuid());
        Assert.Equal("habilitado", AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 0m)).Status);
        var debt = AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 1m));
        Assert.Equal("solo_asistencia", debt.Status);
        Assert.False(debt.MayVote);
        Assert.Equal("MOROSIDAD_GRAN_TESORERIA", debt.ReasonCode);
    }

    [Fact]
    public void Solo_el_bloqueo_levantado_por_Regimen_Interior_habilita_con_saldo_positivo()
    {
        var m = Member(Guid.NewGuid());
        var lift = new AssemblyDebtDecisionEventRecord { AssemblyId = m.AssemblyId, OrganizationId = m.OrganizationId, Action = "lift", Authority = "regimen_interior",
            DecidedBySubject = "ri", Reason = "Convenio de pago", EvidenceReference = "ACTA" };
        Assert.True(AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 500m, debt: [lift])).MayVote);
        var revoke = new AssemblyDebtDecisionEventRecord { AssemblyId = m.AssemblyId, OrganizationId = m.OrganizationId, Action = "revoke", Authority = "regimen_interior",
            DecidedBySubject = "ri", Reason = "Incumplió convenio", EvidenceReference = "ACTA2", ReversesEventId = lift.Id };
        Assert.False(AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 500m, debt: [lift, revoke])).MayVote);
    }

    [Fact]
    public void Lo_que_falta_nunca_habilita_queda_pendiente()
    {
        var m = Member(Guid.NewGuid());
        Assert.Equal("pendiente", AssemblyClosureEngine.Decide(Asm(), m, Ev(m, null)).Status);
        var noHonor = AllAttestations(); noHonor.Remove("honor_coverage");
        Assert.Equal("pendiente", AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 0m, att: noHonor)).Status);
        Assert.Equal("inhabilitado", AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 0m, active: false)).Status);
    }

    [Fact]
    public void Una_restriccion_de_sufragio_deja_solo_asistencia_y_una_de_asistencia_inhabilita()
    {
        var m = Member(Guid.NewGuid());
        InstitutionalRestrictionRecord R(bool attend, bool vote) => new() { PersonId = m.PersonId, SourceAuthority = "regimen_interior", RestrictionType = "x",
            AffectsAttendance = attend, AffectsSuffrage = vote, AffectsInstitutionalRights = false, StartsOn = Cutoff.AddDays(-30), EvidenceReference = "E", RecordedBySubject = "ri" };
        Assert.Equal("solo_asistencia", AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 0m, restrictions: [R(false, true)])).Status);
        Assert.Equal("inhabilitado", AssemblyClosureEngine.Decide(Asm(), m, Ev(m, 0m, restrictions: [R(true, false)])).Status);
    }

    [Fact]
    public void Re_evaluar_sin_cambios_no_agrega_filas_y_un_cambio_encadena_la_anterior()
    {
        var m = Member(Guid.NewGuid());
        var first = AssemblyClosureEngine.NextRecord(Asm(), m, null, Ev(m, 1m), "ri")!;
        Assert.Equal("solo_asistencia", first.Status);
        Assert.Null(AssemblyClosureEngine.NextRecord(Asm(), m, first, Ev(m, 1m), "ri"));
        var second = AssemblyClosureEngine.NextRecord(Asm(), m, first, Ev(m, 0m), "ri")!;
        Assert.Equal("habilitado", second.Status);
        Assert.Equal(first.Id, second.SupersedesEligibilityId);
    }

    [Fact]
    public void El_preflight_acepta_solo_con_todo_evaluado_y_da_un_resumen_estable_sin_datos_personales()
    {
        var m = Member(Guid.NewGuid()); var a = Asm(); m.AssemblyId = a.Id; var ev = Ev(m, 0m);
        var elig = AssemblyClosureEngine.NextRecord(a, m, null, ev, "ri")!;
        var latest = new Dictionary<Guid, AssemblyEligibilityRecord> { [m.Id] = elig };
        var now = DateTimeOffset.UtcNow.AddMinutes(1); var day = DateOnly.FromDateTime(now.UtcDateTime);
        var ok = AssemblyClosureEngine.Preflight(a, [m], latest, ev, "ACTA-CIERRE", now, day);
        Assert.True(ok.DocumentaryPreflightReady); Assert.Equal(1, ok.VotersCount);
        Assert.Matches("^[0-9a-f]{64}$", ok.CandidateSnapshotDigestSha256);
        Assert.Equal(ok.CandidateSnapshotDigestSha256, AssemblyClosureEngine.Preflight(a, [m], latest, ev, "ACTA-CIERRE", now, day).CandidateSnapshotDigestSha256);

        var noBalance = Ev(m, null);
        Assert.False(AssemblyClosureEngine.Preflight(a, [m], latest, noBalance, "ACTA-CIERRE", now, day).DocumentaryPreflightReady);
        var pending = AssemblyClosureEngine.NextRecord(a, m, null, noBalance, "ri")!;
        Assert.False(AssemblyClosureEngine.Preflight(a, [m], new Dictionary<Guid, AssemblyEligibilityRecord> { [m.Id] = pending }, noBalance, "ACTA-CIERRE", now, day).DocumentaryPreflightReady);
        Assert.False(AssemblyClosureEngine.Preflight(a, [m], latest, ev, "", now, day).DocumentaryPreflightReady);
    }

    [Fact]
    public void El_saldo_solo_existe_cuando_Tesoreria_y_Hospitalaria_lo_validaron_y_se_suman()
    {
        var org = Guid.NewGuid(); var asm = Guid.NewGuid();
        AssemblyWorkshopBalanceEventRecord B(string source, decimal amount, string currency = "CLP") => new()
        { AssemblyId = asm, OrganizationId = org, Source = source, AmountPending = amount, Currency = currency, SourceReference = "REF-" + source, RecordedBySubject = "x" };
        var gt = B(AssemblyClosureEngine.SourceTreasury, 0m);
        Assert.Empty(AssemblyClosureEngine.CurrentBalances([gt]));
        var hosp = B(AssemblyClosureEngine.SourceHospitalaria, 2500m);
        Assert.Equal(2500m, AssemblyClosureEngine.CurrentBalances([gt, hosp])[org].AmountPending);
        Assert.Empty(AssemblyClosureEngine.CurrentBalances([gt, B(AssemblyClosureEngine.SourceHospitalaria, 1m, "USD")]));
        var fix = B(AssemblyClosureEngine.SourceHospitalaria, 0m); fix.SupersedesEventId = hosp.Id;
        Assert.Equal(0m, AssemblyClosureEngine.CurrentBalances([gt, hosp, fix])[org].AmountPending);
    }
}
