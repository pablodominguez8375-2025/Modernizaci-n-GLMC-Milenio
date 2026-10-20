using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Control estricto para decisiones RI con fuente y razón.
/// La habilitación final NO se puede aprobar a través de este circuito.
/// El endpoint futuro debe agregar auditoría transaccional y autorización.
/// </summary>
public static class AssemblyReviewDecisionAdmissionPolicy
{
    public const string RequestCorrection = "request_correction";
    public const string ReferValidation = "refer_validation";
    public const string ConfirmIneligible = "confirm_ineligible";
    public const string AttendanceOnly = "attendance_only";

    public static AssemblyReviewDecisionEventRecord? Prepare(
        ClaimsPrincipal actor,
        AssemblyRecord assembly,
        AssemblyMemberStatusRecord member,
        string? action, string? reason, string? evidenceReference,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(member);
        var subject = actor.FindFirst("sub")?.Value;

        if (actor.Identity?.IsAuthenticated != true ||
            !actor.HasClaim(InstitutionalClaims.Scope, "order") ||
            !actor.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior) ||
            string.IsNullOrWhiteSpace(subject) || subject.Length > 320 ||
            assembly.Id == Guid.Empty || assembly.Status != "draft" ||
            assembly.ClosedAtUtc is not null ||
            assembly.CutoffDate > assembly.MeetingDate ||
            member.AssemblyId != assembly.Id || member.Id == Guid.Empty ||
            member.PersonId == Guid.Empty ||
            action is not (RequestCorrection or ReferValidation or ConfirmIneligible or AttendanceOnly) ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000 ||
            string.IsNullOrWhiteSpace(evidenceReference) ||
            evidenceReference.Trim().Length > 500 ||
            nowUtc == default)
            return null;

        return new AssemblyReviewDecisionEventRecord
        {
            AssemblyId = assembly.Id,
            MemberStatusId = member.Id,
            Action = action,
            Reason = reason.Trim(),
            EvidenceReference = evidenceReference.Trim(),
            DecidedBySubject = subject,
            OccurredAtUtc = nowUtc
        };
    }
}
