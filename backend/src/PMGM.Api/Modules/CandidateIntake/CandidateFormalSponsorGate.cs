using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// Acredita al Maestro/a patrocinante del Taller receptor inmediatamente antes
/// de publicar o remitir una solicitud formal. No utiliza PresentersJson ni el
/// recomendante del origen como evidencia de patrocinio.
/// </summary>
public static class CandidateFormalSponsorGate
{
    public static async Task<CandidateFormalSponsorDecision> EvaluateAsync(
        PmgmDbContext db, CeremonyRequest ceremony,
        DateOnly asOf, CancellationToken ct)
    {
        // Expedientes anteriores al campo de origen requieren regularización
        // institucional; no inferir un patrocinante histórico ni bloquear el
        // circuito legado por datos que no se recababan al momento del ingreso.
        if (ceremony.CandidateOriginKind is null)
            return CandidateFormalSponsorDecision.Allowed();

        if (ceremony.CandidateSponsorMemberId is not Guid sponsorId || sponsorId == Guid.Empty)
            return CandidateFormalSponsorDecision.Blocked(
                "CANDIDATE_WORKSHOP_SPONSOR_REQUIRED",
                "Antes de publicar o remitir la solicitud, debe registrar un Maestro/a patrocinante del Taller receptor.");

        var verified = await CandidateOriginAccreditation.VerifyWorkshopSponsorAsync(
            db, sponsorId, ceremony.OrganizationId, asOf, ct);
        if (verified is null || !verified.IsActive)
            return CandidateFormalSponsorDecision.Blocked(
                "CANDIDATE_SPONSOR_NOT_ELIGIBLE_IN_WORKSHOP",
                "El Maestro/a patrocinante debe conservar grado de Maestro y membresía activa en el Taller receptor.");

        return CandidateFormalSponsorDecision.Allowed();
    }

    public static IResult Conflict(CandidateFormalSponsorDecision decision)
        => Results.Conflict(new { code = decision.Code, message = decision.Message });
}

public sealed record CandidateFormalSponsorDecision(
    bool CanProceed, string? Code, string? Message)
{
    public static CandidateFormalSponsorDecision Allowed() => new(true, null, null);
    public static CandidateFormalSponsorDecision Blocked(string code, string message)
        => new(false, code, message);
}
