using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Evaluación y verificación de cierre de padrón, PURA (sin base de datos). Combina SOLO evidencia ya registrada
/// por Régimen Interior (constancias con acta, saldos informados por oficio de Gran Tesorería, restricciones,
/// bloqueos levantados) con las políticas existentes. Fail-closed: lo que falta deja al asambleísta «pendiente»
/// y el cierre se rechaza. Nunca recibe afirmaciones desde el navegador.
/// </summary>
public static class AssemblyClosureEngine
{
    public static readonly string[] RequiredAttestations =
        [AssemblyAttestationPolicy.MasterPeriods, AssemblyAttestationPolicy.Membership,
         AssemblyAttestationPolicy.HonorCoverage, AssemblyAttestationPolicy.RegimenCoverage];

    public const string SourceTreasury = "gran_tesoreria";
    public const string SourceHospitalaria = "gran_hospitalaria";

    /// <summary>
    /// Saldo vigente por Taller con la Gran Logia: suma lo que valida Gran Tesorería y lo que valida Gran Hospitalaria
    /// (reposiciones por hermanos fallecidos y cuota anual). Solo existe si AMBOS responsables ya lo validaron y en la misma moneda.
    /// </summary>
    public static Dictionary<Guid, AssemblyWorkshopBalanceEventRecord> CurrentBalances(IEnumerable<AssemblyWorkshopBalanceEventRecord> events)
    {
        var all = events.ToArray();
        var superseded = all.Where(b => b.SupersedesEventId is not null).Select(b => b.SupersedesEventId!.Value).ToHashSet();
        var current = all.Where(b => !superseded.Contains(b.Id)).GroupBy(b => (b.OrganizationId, b.Source))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OccurredAtUtc).First());
        var result = new Dictionary<Guid, AssemblyWorkshopBalanceEventRecord>();
        foreach (var org in current.Keys.Select(k => k.OrganizationId).Distinct())
        {
            if (!current.TryGetValue((org, SourceTreasury), out var t) || !current.TryGetValue((org, SourceHospitalaria), out var h) || t.Currency != h.Currency) continue;
            var reference = $"GT: {t.SourceReference} · GH: {h.SourceReference}";
            result[org] = new AssemblyWorkshopBalanceEventRecord
            {
                Id = t.Id, AssemblyId = t.AssemblyId, OrganizationId = org, Source = SourceTreasury, AmountPending = t.AmountPending + h.AmountPending,
                Currency = t.Currency, SourceReference = reference.Length > 500 ? reference[..500] : reference,
                RecordedBySubject = t.RecordedBySubject, CutoffDate = t.CutoffDate, OccurredAtUtc = t.OccurredAtUtc > h.OccurredAtUtc ? t.OccurredAtUtc : h.OccurredAtUtc
            };
        }
        return result;
    }

    public static string[] MissingAttestations(IReadOnlyDictionary<string, AssemblyAttestationEventRecord> current) =>
        RequiredAttestations.Where(k => !current.ContainsKey(k)).ToArray();

    /// <summary>Evidencia vigente de una Asamblea, ya resuelta desde la base de datos.</summary>
    public sealed record Evidence(
        IReadOnlyDictionary<string, AssemblyAttestationEventRecord> Attestations,
        IReadOnlyDictionary<Guid, AssemblyWorkshopBalanceEventRecord> Balances,
        IReadOnlyCollection<InstitutionalRestrictionRecord> Restrictions,
        IReadOnlyCollection<AssemblyDebtDecisionEventRecord> DebtEvents,
        IReadOnlyDictionary<Guid, bool> MemberActiveAtCutoff);

    public static AssemblyDebtDecisionEventRecord? ActiveLift(
        IEnumerable<AssemblyDebtDecisionEventRecord> events, Guid organizationId)
    {
        var mine = events.Where(e => e.OrganizationId == organizationId).ToArray();
        var reversed = mine.Where(e => e.Action == AssemblyDebtBlockEndpoints.Revoke && e.ReversesEventId is not null)
            .Select(e => e.ReversesEventId!.Value).ToHashSet();
        return mine.Where(e => e.Action == AssemblyDebtBlockEndpoints.Lift && !reversed.Contains(e.Id))
            .OrderByDescending(e => e.OccurredAtUtc).FirstOrDefault();
    }

    /// <summary>Decisión vigente de un asambleísta con la evidencia disponible.</summary>
    public static AssemblyEligibilityDecision Decide(
        AssemblyRecord assembly, AssemblyMemberStatusRecord member, Evidence ev)
    {
        var cutoff = assembly.CutoffDate;
        var person = member.PersonId;
        AssemblyAttestationEventRecord? Att(string k) => ev.Attestations.GetValueOrDefault(k);
        var membership = Att(AssemblyAttestationPolicy.Membership);
        var honor = Att(AssemblyAttestationPolicy.HonorCoverage);
        var regimen = Att(AssemblyAttestationPolicy.RegimenCoverage);

        var rights = AssemblyInstitutionalRightsEvidencePolicy.Evaluate(
            person, member.OrganizationId, cutoff,
            membership is null ? null : new AssemblyMembershipRightsEvidence(
                person, member.OrganizationId, cutoff,
                ev.MemberActiveAtCutoff.GetValueOrDefault(member.MemberId, false), true, true, membership.ActaReference),
            honor is null ? null : new AssemblyRightsSourceCoverage(person, cutoff, "tribunal_honor", true, true, honor.ActaReference),
            regimen is null ? null : new AssemblyRightsSourceCoverage(person, cutoff, "regimen_interior", true, true, regimen.ActaReference),
            ev.Restrictions.Where(r => r.PersonId == person).Select(r => new AssemblyRightsRestrictionEvidence(
                person, r.SourceAuthority, r.StartsOn, r.EndsOn,
                r.RevokedAtUtc is null ? null : DateOnly.FromDateTime(r.RevokedAtUtc.Value.UtcDateTime),
                r.AffectsAttendance, r.AffectsSuffrage, r.AffectsInstitutionalRights, true, r.EvidenceReference)).ToArray());

        var balance = ev.Balances.GetValueOrDefault(member.OrganizationId);
        var lift = ActiveLift(ev.DebtEvents, member.OrganizationId);
        return AssemblyEvidencedEligibilityPolicy.Evaluate(
            member.OrganizationId, cutoff,
            new AssemblyMemberCategoryDecision(member.Category, true,
                member.Category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember ? 1 : 0,
                member.Category == AssemblyMemberCategoryPolicy.Representative, "Acreditada con la constancia de períodos de Venerable Maestro."),
            rights,
            balance is null ? null : new AssemblyGrandTreasuryBalance(
                member.OrganizationId, cutoff, AssemblyVoterEligibilityPolicy.GrandTreasurySource,
                balance.SourceReference, balance.Currency, balance.AmountPending, true),
            lift is null ? null : new AssemblyDebtException(
                member.OrganizationId, AssemblyVoterEligibilityPolicy.RegimenInteriorAuthority,
                lift.DecidedBySubject,
                // La decisión de Régimen Interior rige para ESTA Asamblea aunque se tome después del corte
                // (regla del PO); la política exige fecha ≤ corte, por eso se acota al corte.
                Min(DateOnly.FromDateTime(lift.OccurredAtUtc.UtcDateTime), cutoff), lift.Reason, lift.EvidenceReference, null));
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    /// <summary>Nueva fila de evaluación si la decisión cambió (o aún no hay una evaluada); null si ya está vigente.</summary>
    public static AssemblyEligibilityRecord? NextRecord(
        AssemblyRecord assembly, AssemblyMemberStatusRecord member, AssemblyEligibilityRecord? latest,
        Evidence ev, string actor)
    {
        var decision = Decide(assembly, member, ev);
        var balance = ev.Balances.GetValueOrDefault(member.OrganizationId);
        var regimen = ev.Attestations.GetValueOrDefault(AssemblyAttestationPolicy.RegimenCoverage);
        var honor = ev.Attestations.GetValueOrDefault(AssemblyAttestationPolicy.HonorCoverage);
        var next = new AssemblyEligibilityRecord
        {
            AssemblyId = assembly.Id, MemberStatusId = member.Id,
            Status = decision.Status, MayAttend = decision.MayAttend, MayVote = decision.MayVote, ReasonCode = decision.ReasonCode,
            GrandTreasuryPendingAmount = balance?.AmountPending, GrandTreasuryCurrency = balance?.Currency,
            GrandTreasurySourceReference = balance?.SourceReference,
            GrandTreasuryAsOfDate = balance is null ? null : assembly.CutoffDate,
            InstitutionalRightsSourceReference = regimen?.ActaReference, HonorRightsSourceReference = honor?.ActaReference,
            EvaluatedBySubject = actor, SupersedesEligibilityId = latest?.Id
        };
        if (latest is not null && Same(latest, next)) return null;
        return next;
    }

    private static bool Same(AssemblyEligibilityRecord a, AssemblyEligibilityRecord b) =>
        a.Status == b.Status && a.MayAttend == b.MayAttend && a.MayVote == b.MayVote && a.ReasonCode == b.ReasonCode &&
        a.GrandTreasuryPendingAmount == b.GrandTreasuryPendingAmount && a.GrandTreasuryCurrency == b.GrandTreasuryCurrency &&
        a.GrandTreasurySourceReference == b.GrandTreasurySourceReference &&
        a.GrandTreasuryAsOfDate == b.GrandTreasuryAsOfDate &&
        a.InstitutionalRightsSourceReference == b.InstitutionalRightsSourceReference &&
        a.HonorRightsSourceReference == b.HonorRightsSourceReference;

    /// <summary>Verificación final con la política de preflight existente (resumen determinista sin RUT ni nombres).</summary>
    public static AssemblyClosurePreflightResult Preflight(
        AssemblyRecord assembly, IReadOnlyCollection<AssemblyMemberStatusRecord> members,
        IReadOnlyDictionary<Guid, AssemblyEligibilityRecord> latest, Evidence ev,
        string rosterReference, DateTimeOffset evaluatedAsOfUtc, DateOnly operationDay)
    {
        var attestationsOk = MissingAttestations(ev.Attestations).Length == 0;
        string Acta(string k) => ev.Attestations.GetValueOrDefault(k)?.ActaReference ?? "";
        var candidates = new List<AssemblyClosureCandidateEvidence>();
        foreach (var m in members)
        {
            if (!latest.TryGetValue(m.Id, out var elig)) continue;
            var balance = ev.Balances.GetValueOrDefault(m.OrganizationId);
            candidates.Add(new AssemblyClosureCandidateEvidence(m, elig, new AssemblyClosureMemberSources(
                m.PersonId, m.OrganizationId, assembly.CutoffDate,
                attestationsOk, attestationsOk, balance is not null, attestationsOk, attestationsOk,
                elig.Status != AssemblyVoterEligibilityPolicy.Pending,
                Acta(AssemblyAttestationPolicy.Membership), Acta(AssemblyAttestationPolicy.MasterPeriods),
                balance?.SourceReference ?? "", balance?.Currency ?? "",
                Acta(AssemblyAttestationPolicy.HonorCoverage), Acta(AssemblyAttestationPolicy.RegimenCoverage),
                ActiveLift(ev.DebtEvents, m.OrganizationId) is not null), evaluatedAsOfUtc));
        }
        var allBalances = members.Select(m => m.OrganizationId).Distinct().All(o => ev.Balances.ContainsKey(o));
        var coverage = new AssemblyClosureOfficialCoverage(assembly.Id, assembly.CutoffDate,
            attestationsOk && members.Count > 0,
            members.Count > 0 && members.All(m => latest.TryGetValue(m.Id, out var e) && e.Status != AssemblyVoterEligibilityPolicy.Pending),
            attestationsOk && allBalances, rosterReference);
        return AssemblyClosurePreflightPolicy.Assess(assembly, candidates, coverage, operationDay);
    }
}
