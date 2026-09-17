using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeCouncilPolicy
{
    private static readonly HashSet<string> CouncilRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        InstitutionalRoles.TallerVenerable,
        InstitutionalRoles.TallerInmediatoExVenerable,
        InstitutionalRoles.TallerPrimerVigilante,
        InstitutionalRoles.TallerSegundoVigilante,
        InstitutionalRoles.TallerOrador,
        InstitutionalRoles.TallerSecretaria,
        InstitutionalRoles.TallerTesoreria,
        InstitutionalRoles.TallerHospitalaria
    };

    public static bool IsCouncilRole(string? role)
        => !string.IsNullOrWhiteSpace(role) && CouncilRoles.Contains(role);

    public static bool CanVote(string participationType, string? role)
        => participationType == LodgeCouncilCodes.ParticipationType.Member && IsCouncilRole(role);

    public static bool RequiresChamberReview(string category)
        => category is LodgeCouncilCodes.DecisionCategory.BudgetProposal or
            LodgeCouncilCodes.DecisionCategory.InstructionProgramProposal or
            LodgeCouncilCodes.DecisionCategory.OfficerChangeProposal or
            LodgeCouncilCodes.DecisionCategory.ForcedWithdrawalProposal;

    public static bool IsOutcomeCompatible(string category, string outcome)
    {
        if (!LodgeCouncilCodes.DecisionOutcome.IsValid(outcome)) return false;

        if (RequiresChamberReview(category))
            return outcome is LodgeCouncilCodes.DecisionOutcome.ApprovedForReferral or LodgeCouncilCodes.DecisionOutcome.Rejected;

        return outcome is LodgeCouncilCodes.DecisionOutcome.Approved or
            LodgeCouncilCodes.DecisionOutcome.Rejected or
            LodgeCouncilCodes.DecisionOutcome.Recorded;
    }

    // El Reglamento vigente exige "quórum calificado" para el Consejo de Administración,
    // pero el capítulo 10 consultado no fija un número. El sistema registra una confirmación
    // institucional auditable y no inventa un umbral numérico.
    public static bool CanConfirmQualifiedQuorum(bool explicitlyConfirmed, int presentVotingMembers)
        => explicitlyConfirmed && presentVotingMembers > 0;
}
