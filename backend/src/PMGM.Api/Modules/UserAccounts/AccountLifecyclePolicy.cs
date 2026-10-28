using System.Security.Claims;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.UserAccounts;

/// <summary>
/// Política de seguridad central para el ciclo de vida de cuentas (#768).
/// Esta fase es deliberadamente fail-closed: no ejecuta mutaciones Keycloak.
/// Una cuenta que no ha pasado comprobaciones *vivas* de roles y de sesión
/// nunca puede activarse, desactivarse ni eliminarse por una pantalla.
/// </summary>
public static class AccountLifecyclePolicy
{
    // El motivo se comprueba en servidor y nunca contiene información de
    // credenciales o sesiones. Ninguna mutación futura debe depender
    // únicamente de una validación realizada en el navegador.
    public static AccountLifecycleDecision ValidateReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? "";
        if (trimmed.Length is < 10 or > 200 ||
            trimmed.Any(c => char.IsControl(c)))
            return AccountLifecycleDecision.Deny("invalid_reason");
        return AccountLifecycleDecision.Allow();
    }

    public static bool IsUniqueMaster(ClaimsPrincipal principal, IInstitutionalAccessService access)
        => access.CanConfigureSystem(principal) &&
           access.IsPlatformSuperAdmin(principal) &&
           principal.Claims.Any(c => c.Type == "preferred_username" &&
               string.Equals(c.Value, "admin", StringComparison.OrdinalIgnoreCase));

    public static AccountLifecycleDecision Evaluate(
        AccountLifecycleAction action,
        AccountInventoryItem? target,
        string? actorSubject,
        AccountLifecycleEvidence evidence,
        string? confirmation = null)
    {
        if (target is null || !Guid.TryParse(target.Subject, out _))
            return AccountLifecycleDecision.Deny("unknown");
        // El Máster bootstrap real (admin) NUNCA se modifica por la API normal.
        // La excepción aprobada para qa.admin aplica SOLO a eliminación definitiva,
        // nunca a activación/desactivación ni a otras cuentas históricas.
        if (string.Equals(target.Username, "admin", StringComparison.OrdinalIgnoreCase))
            return AccountLifecycleDecision.Deny("protected");
        if (string.Equals(target.Subject, actorSubject, StringComparison.OrdinalIgnoreCase))
            return AccountLifecycleDecision.Deny("self");

        var qaAdminException =
            action == AccountLifecycleAction.DeletePermanently &&
            string.Equals(target.Username, "qa.admin", StringComparison.OrdinalIgnoreCase) &&
            target.Protected && !target.Managed &&
            target.Origin == "provisioned" && target.Kind == "legacy_protected";
        if (qaAdminException)
        {
            // Evidencia exclusiva del backend: comprobar el subject vivo en
            // Keycloak y demostrar que el único Máster admin sigue existiendo
            // como otra identidad. El navegador NO puede suministrar estos flags.
            if (!evidence.LegacyQaAdminSubjectVerified ||
                !evidence.UniqueMasterSubjectVerifiedDistinct ||
                !evidence.EffectiveRoleEvidenceComplete)
                return AccountLifecycleDecision.Deny("legacy_exception_unverified");
        }
        else if (!target.Managed || target.Protected || target.Origin != "managed" ||
            target.Kind is not ("member" or "superuser") ||
            string.Equals(target.Username, "qa.admin", StringComparison.OrdinalIgnoreCase))
            return AccountLifecycleDecision.Deny("protected");

        // El atributo pmgm_managed no basta. Solo la excepción de qa.admin,
        // con verificación independiente de Máster y roles efectivos, puede
        // superar un rol histórico privilegiado.
        if (!evidence.RolesReadFromKeycloak ||
            (evidence.HasPrivilegedEffectiveRole && !qaAdminException) ||
            !evidence.EffectiveRoleEvidenceComplete)
            return AccountLifecycleDecision.Deny("role_unverified");
        if (!evidence.IdentityStateVerified)
            return AccountLifecycleDecision.Deny("state_unverified");
        if (action == AccountLifecycleAction.Activate)
        {
            if (target.Enabled) return AccountLifecycleDecision.Deny("already_active");
            if (!evidence.CurrentEntitlementVerified)
                return AccountLifecycleDecision.Deny("entitlement_unverified");
            return AccountLifecycleDecision.Allow();
        }
        if (!target.Enabled && action == AccountLifecycleAction.Deactivate)
            return AccountLifecycleDecision.Deny("already_inactive");
        // Logout Keycloak por sí solo NO revoca todos los JWT de acceso emitidos.
        // Debe existir un gate demostrable de rechazo de tokens residuales en API.
        if (!evidence.RevocationGuaranteed)
            return AccountLifecycleDecision.Deny("revocation_unverified");
        if (action == AccountLifecycleAction.DeletePermanently)
        {
            if (confirmation != target.Username)
                return AccountLifecycleDecision.Deny("confirmation_mismatch");
            if (!evidence.HistoricalDataRetentionVerified)
                return AccountLifecycleDecision.Deny("retention_unverified");
        }
        return AccountLifecycleDecision.Allow();
    }
}

public enum AccountLifecycleAction { Activate, Deactivate, DeletePermanently }

/// <summary>Pruebas vivas requeridas; ningún campo por defecto permite mutación.</summary>
public sealed record AccountLifecycleEvidence(
    bool RolesReadFromKeycloak = false,
    bool HasPrivilegedEffectiveRole = false,
    bool IdentityStateVerified = false,
    bool CurrentEntitlementVerified = false,
    bool RevocationGuaranteed = false,
    bool HistoricalDataRetentionVerified = false,
    bool LegacyQaAdminSubjectVerified = false,
    bool UniqueMasterSubjectVerifiedDistinct = false,
    bool EffectiveRoleEvidenceComplete = false);

public sealed record AccountLifecycleDecision(bool Allowed, string? DenialCode)
{
    public static AccountLifecycleDecision Allow() => new(true, null);
    public static AccountLifecycleDecision Deny(string reason) => new(false, reason);
}
