using System.Security.Claims;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Comprobación de autoridad institucional EXPLÍCITA. No basta con
/// HasRole() porque ese método admite bypass de administrador de plataforma.
/// La autorización definitiva de escritura además debe pasar por
/// DynamicGrandTreasuryAccess.AllowsAsync(..., "write") en el endpoint futuro.
/// No existe un endpoint de certificación en esta etapa.
/// </summary>
public static class AssemblyGrandTreasuryCertificationAuthority
{
    public static bool HasExplicitGrandTreasuryAuthority(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Identity?.IsAuthenticated == true &&
            user.Claims.Any(x => x.Type == InstitutionalClaims.Scope && x.Value == "order") &&
            user.Claims.Any(x => x.Type == InstitutionalClaims.Role &&
                x.Value == InstitutionalRoles.GranTesoreria) &&
            !string.IsNullOrWhiteSpace(user.FindFirst("sub")?.Value);
    }
}

/// <summary>
/// Prevalidación documental de una eventual certificación GT.
/// Nunca construye un AssemblyGrandTreasuryBalance ni permite sufragio.
/// Los bool de integridad documental son declaraciones que un funcionario debe
/// respaldar con evidencias y una aprobación separada; no son autenticación.
/// </summary>
public static class AssemblyGrandTreasuryCertificationPreflight
{
    public static AssemblyGrandTreasuryPreflightResult Assess(
        ClaimsPrincipal actor,
        AssemblyTreasuryInspectionResult inspection,
        AssemblyGrandTreasuryEvidenceProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        ArgumentNullException.ThrowIfNull(proposal);
        var issues = new HashSet<string>(StringComparer.Ordinal);

        if (!AssemblyGrandTreasuryCertificationAuthority.HasExplicitGrandTreasuryAuthority(actor))
            issues.Add("NOT_EXPLICIT_GRAND_TREASURY_AUTHORITY");

        if (proposal.OrganizationId == Guid.Empty || proposal.OrganizationId != inspection.OrganizationId ||
            proposal.CoverageFrom != inspection.CoverageFrom || proposal.Cutoff != inspection.Cutoff ||
            proposal.CoverageFrom > proposal.Cutoff)
            issues.Add("SCOPE_OR_CUTOFF_MISMATCH");

        if (proposal.Currency is null || proposal.Currency.Length != 3 ||
            proposal.Currency.Any(x => x is < 'A' or > 'Z'))
            issues.Add("INVALID_CURRENCY");

        if (proposal.OpeningDebtAsOf != proposal.CoverageFrom.AddDays(-1) ||
            proposal.OpeningPendingAmount is null ||
            proposal.OpeningPendingAmount < 0m)
            issues.Add("OPENING_BALANCE_NOT_EVIDENCED");

        if (string.IsNullOrWhiteSpace(proposal.OpeningDebtSourceReference) ||
            string.IsNullOrWhiteSpace(proposal.CertificationActReference) ||
            string.IsNullOrWhiteSpace(proposal.RectificationRegisterReference) ||
            !IsSha256Hex(proposal.EvidenceDigestSha256))
            issues.Add("MISSING_VERIFIABLE_DOCUMENTS");

        if (!proposal.CompleteDebtHistoryReviewed || !proposal.RectificationsReviewed ||
            !proposal.PaymentsAndCurrencyReviewed)
            issues.Add("INSTITUTIONAL_REVIEW_INCOMPLETE");

        if (!inspection.ObservedRowsConsistent ||
            inspection.ObservedStatementDifference != 0m ||
            inspection.BlockingReasons.Count != 1 ||
            inspection.BlockingReasons[0] != "HISTORICAL_CERTIFICATION_REQUIRED")
            issues.Add("MONTHLY_STATEMENTS_NOT_READY");

        return new AssemblyGrandTreasuryPreflightResult(
            issues.Count == 0, false,
            issues.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static bool IsSha256Hex(string? value) =>
        value is { Length: 64 } && value.All(x =>
            x is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}

public sealed record AssemblyGrandTreasuryEvidenceProposal(
    Guid OrganizationId,
    DateOnly CoverageFrom,
    DateOnly Cutoff,
    string Currency,
    DateOnly OpeningDebtAsOf,
    decimal? OpeningPendingAmount,
    string OpeningDebtSourceReference,
    string CertificationActReference,
    string RectificationRegisterReference,
    string EvidenceDigestSha256,
    bool CompleteDebtHistoryReviewed,
    bool RectificationsReviewed,
    bool PaymentsAndCurrencyReviewed);

public sealed record AssemblyGrandTreasuryPreflightResult(
    bool DocumentsReadyForIndependentReview,
    bool MayAuthorizeSuffrage,
    IReadOnlyList<string> BlockingReasons);
