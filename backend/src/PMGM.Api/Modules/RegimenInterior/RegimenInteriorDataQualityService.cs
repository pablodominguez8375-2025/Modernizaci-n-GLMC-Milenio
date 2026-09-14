using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.RegimenInterior;

public interface IRegimenInteriorDataQualityService
{
    Task<DataQualityResponse> QueryAsync(DataQualityQuery query, CancellationToken cancellationToken);
}

public sealed class RegimenInteriorDataQualityService(PmgmDbContext db) : IRegimenInteriorDataQualityService
{
    public async Task<DataQualityResponse> QueryAsync(DataQualityQuery query, CancellationToken cancellationToken)
    {
        var relatedMemberships = db.Memberships
            .AsNoTracking()
            .Where(x => x.StartDate <= query.AsOf);

        if (query.OrganizationId is not null)
        {
            relatedMemberships = relatedMemberships.Where(x => x.OrganizationId == query.OrganizationId.Value);
        }

        var memberIds = await relatedMemberships
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (memberIds.Count == 0)
        {
            return Empty(query.AsOf);
        }

        var members = await db.Members
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.Id))
            .Select(x => new MemberIdentity(
                x.Id,
                x.InstitutionalNumber,
                x.Person.FirstNames + " " + x.Person.LastNames))
            .ToListAsync(cancellationToken);

        var memberships = await db.Memberships
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.StartDate <= query.AsOf)
            .Select(x => new MembershipRow(
                x.Id,
                x.MemberId,
                x.OrganizationId,
                x.Organization.Name,
                x.Organization.Number,
                x.StartDate,
                x.EndDate,
                x.Status))
            .ToListAsync(cancellationToken);

        var degrees = await db.DegreeEvents
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.EffectiveDate <= query.AsOf)
            .Select(x => new DegreeRow(
                x.Id,
                x.MemberId,
                x.OrganizationId,
                x.Organization.Name,
                x.EventType,
                x.Degree,
                x.EffectiveDate,
                x.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        var statuses = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.EffectiveDate <= query.AsOf)
            .Select(x => new StatusRow(
                x.Id,
                x.MemberId,
                x.OrganizationId,
                x.Organization == null ? null : x.Organization.Name,
                x.EventType,
                x.EffectiveDate,
                x.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        var offices = await db.OfficeAssignments
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.StartDate <= query.AsOf)
            .Select(x => new OfficeRow(
                x.Id,
                x.MemberId,
                x.OrganizationId,
                x.Organization.Name,
                x.OfficeType,
                x.StartDate,
                x.EndDate))
            .ToListAsync(cancellationToken);

        var transfers = await db.MemberTransfers
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.RequestedDate <= query.AsOf)
            .Select(x => new TransferRow(
                x.Id,
                x.MemberId,
                x.SourceOrganizationId,
                x.SourceOrganization.Name,
                x.TargetOrganizationId,
                x.TargetOrganization.Name,
                x.SourceMembership.StartDate,
                x.SourceMembership.EndDate,
                x.TargetMembershipId,
                x.TargetMembership == null ? null : x.TargetMembership.StartDate,
                x.RequestedDate,
                x.ProposedEffectiveDate,
                x.ApprovedEffectiveDate,
                x.Status))
            .ToListAsync(cancellationToken);

        var issues = new List<DataQualityIssue>();
        foreach (var member in members)
        {
            var memberMemberships = memberships
                .Where(x => x.MemberId == member.Id)
                .OrderBy(x => x.StartDate)
                .ThenBy(x => x.EndDate)
                .ToList();
            var memberDegrees = degrees
                .Where(x => x.MemberId == member.Id)
                .OrderBy(x => x.EffectiveDate)
                .ThenBy(x => x.RecordedAtUtc)
                .ToList();
            var memberStatuses = statuses
                .Where(x => x.MemberId == member.Id)
                .OrderBy(x => x.EffectiveDate)
                .ThenBy(x => x.RecordedAtUtc)
                .ToList();
            var memberOffices = offices
                .Where(x => x.MemberId == member.Id)
                .OrderBy(x => x.StartDate)
                .ToList();
            var memberTransfers = transfers
                .Where(x => x.MemberId == member.Id)
                .OrderBy(x => x.RequestedDate)
                .ToList();

            EvaluateMemberships(query.AsOf, member, memberMemberships, issues);
            EvaluateDegreeSequence(member, memberDegrees, issues);
            var deathDate = EvaluateStatusSequence(member, memberStatuses, issues);
            EvaluatePostDeath(member, deathDate, memberMemberships, memberDegrees, memberOffices, issues, query.AsOf);
            EvaluateOffices(member, memberOffices, issues);
            EvaluateTransfers(member, memberTransfers, issues);
            EvaluateActiveWithoutMembership(query.AsOf, member, memberMemberships, memberStatuses, issues);
        }

        IEnumerable<DataQualityIssue> filtered = issues;
        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            filtered = filtered.Where(x => string.Equals(x.Severity, query.Severity, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            filtered = filtered.Where(x => string.Equals(x.Code, query.Code, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = Normalize(query.Search);
            filtered = filtered.Where(x => Normalize($"{x.DisplayName} {x.InstitutionalNumber} {x.OrganizationName} {x.Title} {x.Code}").Contains(term, StringComparison.Ordinal));
        }

        var allIssues = filtered
            .OrderBy(x => SeverityOrder(x.Severity))
            .ThenBy(x => x.DisplayName, StringComparer.Create(new System.Globalization.CultureInfo("es-CL"), true))
            .ThenBy(x => x.Code)
            .ToList();
        var total = allIssues.Count;
        var returned = allIssues.Take(query.Limit).ToList();

        var summary = new DataQualitySummary(
            issues.Count(x => x.Severity == DataQualitySeverity.Error),
            issues.Count(x => x.Severity == DataQualitySeverity.Warning),
            issues.Select(x => x.MemberId).Distinct().Count(),
            issues.GroupBy(x => x.Code)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase));

        return new DataQualityResponse(query.AsOf, total, returned.Count, summary, returned);
    }

    private static void EvaluateMemberships(
        DateOnly asOf,
        MemberIdentity member,
        IReadOnlyList<MembershipRow> rows,
        ICollection<DataQualityIssue> issues)
    {
        foreach (var row in rows.Where(x => x.EndDate is not null && x.EndDate < x.StartDate))
        {
            Add(issues, member, row.OrganizationId, row.OrganizationName,
                "invalid_membership_range", DataQualitySeverity.Error,
                "Afiliación con rango de fechas inválido",
                "La fecha de término de la afiliación es anterior a su fecha de inicio.",
                row.EndDate, row.StartDate,
                "Revisar el acto de ingreso/egreso y su respaldo antes de corregir las fechas.");
        }

        var current = rows.Where(x =>
                x.Status == MembershipCodes.MembershipStatus.Active &&
                x.StartDate <= asOf &&
                (x.EndDate == null || x.EndDate >= asOf))
            .ToList();
        if (current.Count > 1)
        {
            var first = current[0];
            Add(issues, member, first.OrganizationId, first.OrganizationName,
                "multiple_current_memberships", DataQualitySeverity.Error,
                "Más de una afiliación vigente al mismo corte",
                $"Se detectaron {current.Count} afiliaciones activas simultáneas para la fecha de corte.",
                asOf, null,
                "Revisar traslados y cierres de afiliación; no eliminar el historial anterior.");
        }

        for (var i = 0; i < rows.Count; i++)
        {
            for (var j = i + 1; j < rows.Count; j++)
            {
                var left = rows[i];
                var right = rows[j];
                if (left.OrganizationId == right.OrganizationId) continue;
                var leftEnd = Min(left.EndDate ?? asOf, asOf);
                var rightEnd = Min(right.EndDate ?? asOf, asOf);
                if (left.StartDate <= rightEnd && right.StartDate <= leftEnd)
                {
                    Add(issues, member, right.OrganizationId, right.OrganizationName,
                        "overlapping_workshop_memberships", DataQualitySeverity.Warning,
                        "Afiliaciones de Taller superpuestas",
                        $"La afiliación en {right.OrganizationName} se superpone con otra afiliación institucional en el período revisado.",
                        right.StartDate, leftEnd,
                        "Confirmar si corresponde a un traslado, doble pertenencia autorizada o un cierre pendiente.");
                    break;
                }
            }
        }
    }

    private static void EvaluateDegreeSequence(
        MemberIdentity member,
        IReadOnlyList<DegreeRow> rows,
        ICollection<DataQualityIssue> issues)
    {
        var initiation = First(rows, MembershipCodes.DegreeEvent.Initiation);
        var wage = First(rows, MembershipCodes.DegreeEvent.WageIncrease);
        var exaltation = First(rows, MembershipCodes.DegreeEvent.Exaltation);

        if (wage is not null && initiation is null)
        {
            Add(issues, member, wage.OrganizationId, wage.OrganizationName,
                "missing_initiation_before_wage_increase", DataQualitySeverity.Error,
                "Aumento de salario sin iniciación registrada",
                "Existe un aumento de salario, pero no se encontró una iniciación anterior o igual al corte.",
                wage.EffectiveDate, null,
                "Corroborar la fecha de iniciación y registrar el antecedente faltante con evidencia.");
        }
        if (exaltation is not null && wage is null)
        {
            Add(issues, member, exaltation.OrganizationId, exaltation.OrganizationName,
                "missing_wage_increase_before_exaltation", DataQualitySeverity.Error,
                "Exaltación sin aumento de salario registrado",
                "Existe una exaltación, pero no se encontró el aumento de salario previo.",
                exaltation.EffectiveDate, null,
                "Corroborar el aumento de salario y registrar el antecedente faltante con evidencia.");
        }
        if (initiation is not null && wage is not null && wage.EffectiveDate < initiation.EffectiveDate)
        {
            Add(issues, member, wage.OrganizationId, wage.OrganizationName,
                "wage_increase_before_initiation", DataQualitySeverity.Error,
                "Aumento de salario anterior a la iniciación",
                "La secuencia cronológica de grados es imposible según las fechas registradas.",
                wage.EffectiveDate, initiation.EffectiveDate,
                "Contrastar ambas fechas con actas/decretos y corregir sólo con respaldo.");
        }
        if (wage is not null && exaltation is not null && exaltation.EffectiveDate < wage.EffectiveDate)
        {
            Add(issues, member, exaltation.OrganizationId, exaltation.OrganizationName,
                "exaltation_before_wage_increase", DataQualitySeverity.Error,
                "Exaltación anterior al aumento de salario",
                "La fecha de exaltación es anterior al aumento de salario registrado.",
                exaltation.EffectiveDate, wage.EffectiveDate,
                "Contrastar ambas fechas con actas/decretos y corregir sólo con respaldo.");
        }

        foreach (var duplicate in rows.GroupBy(x => x.EventType).Where(x => x.Count() > 1))
        {
            var row = duplicate.OrderBy(x => x.EffectiveDate).First();
            Add(issues, member, row.OrganizationId, row.OrganizationName,
                "duplicate_degree_milestone", DataQualitySeverity.Warning,
                "Hito de grado registrado más de una vez",
                $"Se encontraron {duplicate.Count()} eventos del tipo {row.EventType} para el mismo miembro.",
                row.EffectiveDate, null,
                "Verificar si son duplicados, rectificaciones históricas o actos distintos antes de depurar.");
        }
    }

    private static DateOnly? EvaluateStatusSequence(
        MemberIdentity member,
        IReadOnlyList<StatusRow> rows,
        ICollection<DataQualityIssue> issues)
    {
        var withdrawn = false;
        DateOnly? deathDate = null;

        foreach (var row in rows)
        {
            if (deathDate is not null && row.EffectiveDate > deathDate)
            {
                Add(issues, member, row.OrganizationId, row.OrganizationName,
                    "status_event_after_death", DataQualitySeverity.Error,
                    "Evento institucional posterior a la defunción",
                    $"El evento {row.Status} está fechado después de la defunción registrada.",
                    row.EffectiveDate, deathDate,
                    "Revisar la fecha de defunción y el evento posterior antes de corregir el historial.");
            }

            if (row.Status is MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal or MembershipCodes.InstitutionalStatus.ForcedWithdrawal)
            {
                if (withdrawn)
                {
                    Add(issues, member, row.OrganizationId, row.OrganizationName,
                        "consecutive_withdrawals_without_reinstatement", DataQualitySeverity.Warning,
                        "Retiros consecutivos sin reintegro intermedio",
                        "Se registró un nuevo retiro cuando el miembro ya se encontraba retirado según la secuencia histórica.",
                        row.EffectiveDate, null,
                        "Revisar si existe un reintegro omitido o si uno de los retiros corresponde a una rectificación.");
                }
                withdrawn = true;
            }
            else if (row.Status == MembershipCodes.InstitutionalStatus.Reinstated)
            {
                if (!withdrawn)
                {
                    Add(issues, member, row.OrganizationId, row.OrganizationName,
                        "reinstatement_without_prior_withdrawal", DataQualitySeverity.Warning,
                        "Reintegro sin retiro previo registrado",
                        "El reintegro no tiene un retiro anterior en la secuencia disponible.",
                        row.EffectiveDate, null,
                        "Buscar el retiro histórico faltante o corroborar el tipo correcto del evento.");
                }
                withdrawn = false;
            }
            else if (row.Status == MembershipCodes.InstitutionalStatus.Deceased && deathDate is null)
            {
                deathDate = row.EffectiveDate;
            }
        }

        return deathDate;
    }

    private static void EvaluatePostDeath(
        MemberIdentity member,
        DateOnly? deathDate,
        IReadOnlyList<MembershipRow> memberships,
        IReadOnlyList<DegreeRow> degrees,
        IReadOnlyList<OfficeRow> offices,
        ICollection<DataQualityIssue> issues,
        DateOnly asOf)
    {
        if (deathDate is null) return;

        foreach (var degree in degrees.Where(x => x.EffectiveDate > deathDate))
        {
            Add(issues, member, degree.OrganizationId, degree.OrganizationName,
                "degree_event_after_death", DataQualitySeverity.Error,
                "Hito de grado posterior a la defunción",
                $"El evento de grado {degree.EventType} está fechado después de la defunción.",
                degree.EffectiveDate, deathDate,
                "Corroborar las fechas con los documentos institucionales correspondientes.");
        }

        foreach (var office in offices.Where(x => x.StartDate > deathDate))
        {
            Add(issues, member, office.OrganizationId, office.OrganizationName,
                "office_after_death", DataQualitySeverity.Error,
                "Cargo iniciado después de la defunción",
                $"El cargo {office.OfficeType} comienza después de la defunción registrada.",
                office.StartDate, deathDate,
                "Revisar el período del cargo o la fecha de defunción con evidencia.");
        }

        var activeAfterDeath = memberships.FirstOrDefault(x =>
            x.Status == MembershipCodes.MembershipStatus.Active &&
            x.StartDate <= asOf &&
            (x.EndDate == null || x.EndDate >= asOf));
        if (activeAfterDeath is not null && deathDate <= asOf)
        {
            Add(issues, member, activeAfterDeath.OrganizationId, activeAfterDeath.OrganizationName,
                "active_membership_after_death", DataQualitySeverity.Error,
                "Afiliación vigente después de la defunción",
                "La afiliación continúa activa al corte pese a existir una defunción anterior o igual al corte.",
                asOf, deathDate,
                "Cerrar la afiliación sólo después de validar el registro de defunción y su fecha efectiva.");
        }
    }

    private static void EvaluateOffices(
        MemberIdentity member,
        IReadOnlyList<OfficeRow> rows,
        ICollection<DataQualityIssue> issues)
    {
        foreach (var row in rows.Where(x => x.EndDate is not null && x.EndDate < x.StartDate))
        {
            Add(issues, member, row.OrganizationId, row.OrganizationName,
                "invalid_office_range", DataQualitySeverity.Error,
                "Período de cargo inválido",
                $"El cargo {row.OfficeType} termina antes de su fecha de inicio.",
                row.EndDate, row.StartDate,
                "Corroborar el período oficial del cargo antes de modificarlo.");
        }
    }

    private static void EvaluateTransfers(
        MemberIdentity member,
        IReadOnlyList<TransferRow> rows,
        ICollection<DataQualityIssue> issues)
    {
        foreach (var row in rows)
        {
            if (row.SourceOrganizationId == row.TargetOrganizationId)
            {
                Add(issues, member, row.SourceOrganizationId, row.SourceOrganizationName,
                    "transfer_same_source_and_target", DataQualitySeverity.Error,
                    "Traslado con el mismo Taller de origen y destino",
                    "El Taller de origen y el Taller de destino del traslado son iguales.",
                    row.RequestedDate, null,
                    "Revisar la solicitud y organizaciones vinculadas al traslado.");
            }

            if (row.ProposedEffectiveDate < row.RequestedDate)
            {
                Add(issues, member, row.TargetOrganizationId, row.TargetOrganizationName,
                    "transfer_effective_before_request", DataQualitySeverity.Error,
                    "Fecha propuesta de traslado anterior a la solicitud",
                    "La fecha efectiva propuesta es anterior a la fecha en que se solicitó el traslado.",
                    row.ProposedEffectiveDate, row.RequestedDate,
                    "Corroborar solicitud y fecha efectiva aprobada.");
            }

            if (row.ApprovedEffectiveDate is not null && row.ApprovedEffectiveDate < row.RequestedDate)
            {
                Add(issues, member, row.TargetOrganizationId, row.TargetOrganizationName,
                    "approved_transfer_before_request", DataQualitySeverity.Error,
                    "Traslado aprobado antes de su solicitud",
                    "La fecha efectiva aprobada es anterior a la fecha de solicitud.",
                    row.ApprovedEffectiveDate, row.RequestedDate,
                    "Revisar la resolución de traslado y sus fechas.");
            }

            if (row.Status == MembershipCodes.TransferStatus.Executed)
            {
                var effective = row.ApprovedEffectiveDate ?? row.ProposedEffectiveDate;
                if (row.TargetMembershipId is null || row.TargetMembershipStartDate is null)
                {
                    Add(issues, member, row.TargetOrganizationId, row.TargetOrganizationName,
                        "executed_transfer_without_target_membership", DataQualitySeverity.Error,
                        "Traslado ejecutado sin afiliación de destino",
                        "El traslado figura ejecutado, pero no existe una afiliación de destino vinculada.",
                        effective, null,
                        "Revisar la ejecución del traslado y crear/vincular la afiliación destino sólo con respaldo.");
                }
                else if (row.TargetMembershipStartDate != effective)
                {
                    Add(issues, member, row.TargetOrganizationId, row.TargetOrganizationName,
                        "target_membership_date_mismatch", DataQualitySeverity.Warning,
                        "Fecha de afiliación destino no coincide con el traslado",
                        "La afiliación del Taller destino comienza en una fecha distinta de la fecha efectiva del traslado.",
                        row.TargetMembershipStartDate, effective,
                        "Corroborar resolución y fecha de ingreso al Taller destino.");
                }

                if (effective < row.SourceMembershipStartDate ||
                    (row.SourceMembershipEndDate is not null && effective > row.SourceMembershipEndDate.Value))
                {
                    Add(issues, member, row.SourceOrganizationId, row.SourceOrganizationName,
                        "transfer_outside_source_membership", DataQualitySeverity.Warning,
                        "Traslado fuera del período de afiliación de origen",
                        "La fecha efectiva del traslado queda fuera del período registrado para la afiliación de origen.",
                        effective, row.SourceMembershipEndDate ?? row.SourceMembershipStartDate,
                        "Revisar fechas de afiliación de origen y resolución de traslado.");
                }
            }
        }
    }

    private static void EvaluateActiveWithoutMembership(
        DateOnly asOf,
        MemberIdentity member,
        IReadOnlyList<MembershipRow> memberships,
        IReadOnlyList<StatusRow> statuses,
        ICollection<DataQualityIssue> issues)
    {
        var latestStatus = statuses
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefault();
        var institutionalActive = latestStatus is null ||
                                  latestStatus.Status is MembershipCodes.InstitutionalStatus.Active or MembershipCodes.InstitutionalStatus.Reinstated;
        if (!institutionalActive) return;

        var hasCurrentMembership = memberships.Any(x =>
            x.Status == MembershipCodes.MembershipStatus.Active &&
            x.StartDate <= asOf &&
            (x.EndDate == null || x.EndDate >= asOf));
        if (!hasCurrentMembership)
        {
            Add(issues, member, latestStatus?.OrganizationId, latestStatus?.OrganizationName,
                "active_status_without_current_membership", DataQualitySeverity.Warning,
                "Estado activo sin afiliación vigente",
                "El estado institucional indica actividad o reintegro, pero no existe una afiliación activa al corte.",
                latestStatus?.EffectiveDate ?? asOf, null,
                "Verificar si falta una afiliación, un traslado o un evento de cierre institucional.");
        }
    }

    private static DegreeRow? First(IReadOnlyList<DegreeRow> rows, string eventType)
        => rows.FirstOrDefault(x => x.EventType == eventType);

    private static DateOnly Min(DateOnly left, DateOnly right) => left <= right ? left : right;

    private static int SeverityOrder(string severity)
        => severity == DataQualitySeverity.Error ? 0 : 1;

    private static void Add(
        ICollection<DataQualityIssue> issues,
        MemberIdentity member,
        Guid? organizationId,
        string? organizationName,
        string code,
        string severity,
        string title,
        string description,
        DateOnly? primaryDate,
        DateOnly? relatedDate,
        string suggestedAction)
        => issues.Add(new DataQualityIssue(
            code,
            severity,
            member.Id,
            member.InstitutionalNumber,
            member.DisplayName,
            organizationId,
            organizationName,
            title,
            description,
            primaryDate,
            relatedDate,
            suggestedAction));

    private static string Normalize(string value)
        => string.Concat(value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant()
            .Trim();

    private static DataQualityResponse Empty(DateOnly asOf)
        => new(asOf, 0, 0, new DataQualitySummary(0, 0, 0, new Dictionary<string, int>()), []);

    private sealed record MemberIdentity(Guid Id, string? InstitutionalNumber, string DisplayName);
    private sealed record MembershipRow(Guid Id, Guid MemberId, Guid OrganizationId, string OrganizationName, string? OrganizationNumber, DateOnly StartDate, DateOnly? EndDate, string Status);
    private sealed record DegreeRow(Guid Id, Guid MemberId, Guid OrganizationId, string OrganizationName, string EventType, string Degree, DateOnly EffectiveDate, DateTimeOffset RecordedAtUtc);
    private sealed record StatusRow(Guid Id, Guid MemberId, Guid? OrganizationId, string? OrganizationName, string Status, DateOnly EffectiveDate, DateTimeOffset RecordedAtUtc);
    private sealed record OfficeRow(Guid Id, Guid MemberId, Guid OrganizationId, string OrganizationName, string OfficeType, DateOnly StartDate, DateOnly? EndDate);
    private sealed record TransferRow(Guid Id, Guid MemberId, Guid SourceOrganizationId, string SourceOrganizationName, Guid TargetOrganizationId, string TargetOrganizationName, DateOnly SourceMembershipStartDate, DateOnly? SourceMembershipEndDate, Guid? TargetMembershipId, DateOnly? TargetMembershipStartDate, DateOnly RequestedDate, DateOnly ProposedEffectiveDate, DateOnly? ApprovedEffectiveDate, string Status);
}

public static class DataQualitySeverity
{
    public const string Error = "error";
    public const string Warning = "warning";
}

public sealed record DataQualityQuery(
    DateOnly AsOf,
    Guid? OrganizationId,
    string? Severity,
    string? Code,
    string? Search,
    int Limit);

public sealed record DataQualityResponse(
    DateOnly AsOf,
    int Total,
    int Returned,
    DataQualitySummary Summary,
    IReadOnlyList<DataQualityIssue> Items);

public sealed record DataQualitySummary(
    int Errors,
    int Warnings,
    int AffectedMembers,
    IReadOnlyDictionary<string, int> ByCode);

public sealed record DataQualityIssue(
    string Code,
    string Severity,
    Guid MemberId,
    string? InstitutionalNumber,
    string DisplayName,
    Guid? OrganizationId,
    string? OrganizationName,
    string Title,
    string Description,
    DateOnly? PrimaryDate,
    DateOnly? RelatedDate,
    string SuggestedAction);
