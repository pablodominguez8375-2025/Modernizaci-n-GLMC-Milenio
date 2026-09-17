using System.Globalization;
using System.Text;
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

    private static readonly IReadOnlyDictionary<string, HashSet<string>> OfficeAliases =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [InstitutionalRoles.TallerVenerable] = AliasSet(InstitutionalRoles.TallerVenerable, "venerable", "venerable maestro"),
            [InstitutionalRoles.TallerInmediatoExVenerable] = AliasSet(InstitutionalRoles.TallerInmediatoExVenerable, "inmediato ex venerable", "inmediato ex venerable maestro", "ex venerable", "ex venerable maestro"),
            [InstitutionalRoles.TallerPrimerVigilante] = AliasSet(InstitutionalRoles.TallerPrimerVigilante, "primer vigilante", "primero vigilante"),
            [InstitutionalRoles.TallerSegundoVigilante] = AliasSet(InstitutionalRoles.TallerSegundoVigilante, "segundo vigilante"),
            [InstitutionalRoles.TallerOrador] = AliasSet(InstitutionalRoles.TallerOrador, "orador", "oradora", "orador/a"),
            [InstitutionalRoles.TallerSecretaria] = AliasSet(InstitutionalRoles.TallerSecretaria, "secretario", "secretaria", "secretario/a"),
            [InstitutionalRoles.TallerTesoreria] = AliasSet(InstitutionalRoles.TallerTesoreria, "tesorero", "tesorera", "tesorero/a"),
            [InstitutionalRoles.TallerHospitalaria] = AliasSet(InstitutionalRoles.TallerHospitalaria, "hospitalario", "hospitalaria", "hospitalario/a")
        };

    public static bool IsCouncilRole(string? role)
        => !string.IsNullOrWhiteSpace(role) && CouncilRoles.Contains(role);

    public static bool CanVote(string participationType, string? role)
        => participationType == LodgeCouncilCodes.ParticipationType.Member && IsCouncilRole(role);

    public static bool OfficeTypeMatchesRole(string? officeType, string? role)
    {
        if (string.IsNullOrWhiteSpace(officeType) || string.IsNullOrWhiteSpace(role) ||
            !OfficeAliases.TryGetValue(role, out var aliases))
            return false;

        return aliases.Contains(NormalizeOfficeValue(officeType));
    }

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

    private static HashSet<string> AliasSet(params string[] values)
        => values.Select(NormalizeOfficeValue).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeOfficeValue(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var separatorPending = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0) builder.Append('_');
                builder.Append(character);
                separatorPending = false;
            }
            else
            {
                separatorPending = builder.Length > 0;
            }
        }

        return builder.ToString();
    }
}
