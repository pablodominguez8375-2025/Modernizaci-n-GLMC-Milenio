namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// Issue #613: contrato de dominio para origen de un insinuado.
/// El servidor debe consultar la membresía activa y construir
/// CandidateOriginVerifiedMember; la UI jamás puede declarar esa evidencia.
/// Esta política NO persiste expedientes ni modifica entrevistadores.
/// </summary>
public static class CandidateOriginPolicy
{
    public const string Brother = "brother";
    public const string Internet = "internet";
    public const string SocialMedia = "social_media";
    public const string Other = "other";
    public const string Unregistered = "unregistered"; // Solo lectura legacy.

    public static CandidateOriginValidationResult ValidateNew(
        string? kind, Guid? recommenderMemberId, string? otherDescription,
        CandidateOriginVerifiedMember? verifiedRecommender, DateOnly operationDay)
    {
        var description = otherDescription?.Trim();
        if (kind is not (Brother or Internet or SocialMedia or Other))
            return Invalid("CANDIDATE_ORIGIN_REQUIRED");

        if (kind == Brother)
        {
            if (recommenderMemberId is null || recommenderMemberId == Guid.Empty)
                return Invalid("RECOMMENDER_REQUIRED");
            if (!string.IsNullOrWhiteSpace(description))
                return Invalid("CANDIDATE_ORIGIN_FIELDS_CONFLICT");
            if (verifiedRecommender is null ||
                verifiedRecommender.MemberId != recommenderMemberId ||
                !verifiedRecommender.IsActive ||
                verifiedRecommender.AsOf != operationDay ||
                string.IsNullOrWhiteSpace(verifiedRecommender.SourceReference))
                return Invalid("ACTIVE_RECOMMENDER_NOT_VERIFIED");
            return new(true, Brother, recommenderMemberId, null, null);
        }

        if (recommenderMemberId is not null)
            return Invalid("CANDIDATE_ORIGIN_FIELDS_CONFLICT");

        if (kind is Internet or SocialMedia)
            return !string.IsNullOrWhiteSpace(description)
                ? Invalid("CANDIDATE_ORIGIN_FIELDS_CONFLICT")
                : new(true, Internet, null, null, null);

        if (string.IsNullOrWhiteSpace(description))
            return Invalid("CANDIDATE_ORIGIN_DESCRIPTION_REQUIRED");
        if (description.Length > 200 || description.Any(char.IsControl))
            return Invalid("CANDIDATE_ORIGIN_DESCRIPTION_INVALID");
        return new(true, Other, null, description, null);
    }

    /// <summary>
    /// Se aplica al asignar cada Maestro y al recuperar maestros elegibles.
    /// RecommenderMemberId debe provenir del expediente privado persistido,
    /// nunca de un parámetro suministrado por el cliente.
    /// </summary>
    public static bool CanBeInterviewer(Guid interviewerMemberId, Guid? persistedRecommenderMemberId)
        => interviewerMemberId != Guid.Empty &&
           (persistedRecommenderMemberId is null ||
            interviewerMemberId != persistedRecommenderMemberId.Value);

    private static CandidateOriginValidationResult Invalid(string errorCode)
        => new(false, null, null, null, errorCode);
}

public sealed record CandidateOriginVerifiedMember(
    Guid MemberId, DateOnly AsOf, bool IsActive, string SourceReference);

public sealed record CandidateOriginValidationResult(
    bool IsValid, string? Kind, Guid? RecommenderMemberId,
    string? OtherDescription, string? ErrorCode);
