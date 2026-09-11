using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionCodes
{
    public static class AffiliationMode
    {
        public const string Simple = "simple";
        public const string Activation = "activation";

        public static bool IsValid(string? value)
            => value is Simple or Activation;
    }

    public static class Requirement
    {
        public const string AffiliationMode = "affiliation_mode";
        public const string WithdrawalLetter = "withdrawal_letter";
        public const string WithdrawalLetterHandwrittenSignature = "withdrawal_letter_handwritten_signature";
        public const string LegalizedInitiationEvidence = "legalized_initiation_evidence";
        public const string LegalizedWageIncreaseEvidence = "legalized_wage_increase_evidence";
        public const string LegalizedExaltationEvidence = "legalized_exaltation_evidence";
        public const string DegreeEvidence = "degree_evidence";
        public const string PeaceAndFriendshipPact = "peace_and_friendship_pact";
        public const string GrandMasterSpecialAcceptance = "grand_master_special_acceptance";
        public const string RePresentation = "re_presentation";
    }
}

public sealed record AdmissionEligibilityInput(
    string AdmissionType,
    string? AffiliationMode,
    bool WithdrawalLetterAttached,
    bool WithdrawalLetterHandwrittenSignatureVerified,
    bool LegalizedInitiationEvidenceAttached = false,
    bool WageIncreaseEvidenceApplies = false,
    bool LegalizedWageIncreaseEvidenceAttached = false,
    bool ExaltationEvidenceApplies = false,
    bool LegalizedExaltationEvidenceAttached = false,
    bool DegreeEvidenceAttached = false,
    bool? HasPeaceAndFriendshipPact = null,
    bool GrandMasterSpecialAcceptanceApproved = false,
    DateOnly? PreviousRejectionDate = null,
    DateOnly? NewPresentationDate = null,
    bool? RejectionCausesRemedied = null);

public sealed record AdmissionRequirementResult(
    string Code,
    string Status,
    string Reason);

public sealed record AdmissionEligibilityDecision(
    bool CanProceed,
    string Status,
    IReadOnlyList<AdmissionRequirementResult> Requirements);

public static class AdmissionEligibilityPolicy
{
    public static AdmissionEligibilityDecision Evaluate(AdmissionEligibilityInput input)
    {
        if (input.AdmissionType is not CeremonyCodes.Type.Affiliation and not CeremonyCodes.Type.Incorporation)
            throw new ArgumentException("El tipo de admisión debe ser afiliación o incorporación.", nameof(input));

        var requirements = new List<AdmissionRequirementResult>();

        if (input.AdmissionType == CeremonyCodes.Type.Affiliation)
            requirements.Add(EvaluateAffiliationMode(input.AffiliationMode));

        requirements.Add(EvaluateRequiredBoolean(
            AdmissionCodes.Requirement.WithdrawalLetter,
            input.WithdrawalLetterAttached,
            "La Carta de Retiro Voluntario está adjunta.",
            "La Carta de Retiro Voluntario del Taller de origen es obligatoria."));

        requirements.Add(EvaluateRequiredBoolean(
            AdmissionCodes.Requirement.WithdrawalLetterHandwrittenSignature,
            input.WithdrawalLetterHandwrittenSignatureVerified,
            "Se registró verificación humana de la firma original de puño y letra.",
            "Debe verificarse que el original de la Carta de Retiro Voluntario esté firmado de puño y letra; una firma digitalizada o una imagen insertada no cumple este requisito."));

        if (input.AdmissionType == CeremonyCodes.Type.Affiliation)
            AddRePresentationRequirementIfNeeded(input, requirements);
        else
            AddIncorporationRequirements(input, requirements);

        var rejected = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Rejected);
        var observed = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Observed);
        var status = rejected
            ? "does_not_comply"
            : observed
                ? "observed"
                : "complies";

        return new AdmissionEligibilityDecision(
            CanProceed: status == "complies",
            Status: status,
            Requirements: requirements);
    }

    private static AdmissionRequirementResult EvaluateAffiliationMode(string? mode)
        => AdmissionCodes.AffiliationMode.IsValid(mode)
            ? Approved(AdmissionCodes.Requirement.AffiliationMode, "Se indicó la modalidad de afiliación simple o con activación.")
            : Rejected(AdmissionCodes.Requirement.AffiliationMode, "Debe indicarse si la afiliación es simple o con activación.");

    private static void AddIncorporationRequirements(
        AdmissionEligibilityInput input,
        ICollection<AdmissionRequirementResult> requirements)
    {
        requirements.Add(EvaluateRequiredBoolean(
            AdmissionCodes.Requirement.LegalizedInitiationEvidence,
            input.LegalizedInitiationEvidenceAttached,
            "Consta antecedente legalizado de iniciación emitido por la Obediencia de procedencia.",
            "La incorporación requiere documento legalizado que acredite fecha, logia y Obediencia de la iniciación."));

        if (input.WageIncreaseEvidenceApplies)
        {
            requirements.Add(EvaluateRequiredBoolean(
                AdmissionCodes.Requirement.LegalizedWageIncreaseEvidence,
                input.LegalizedWageIncreaseEvidenceAttached,
                "Consta antecedente legalizado de aumento de salario.",
                "Debe adjuntarse el antecedente legalizado de aumento de salario cuando corresponda."));
        }

        if (input.ExaltationEvidenceApplies)
        {
            requirements.Add(EvaluateRequiredBoolean(
                AdmissionCodes.Requirement.LegalizedExaltationEvidence,
                input.LegalizedExaltationEvidenceAttached,
                "Consta antecedente legalizado de exaltación.",
                "Debe adjuntarse el antecedente legalizado de exaltación cuando corresponda."));
        }

        requirements.Add(EvaluateRequiredBoolean(
            AdmissionCodes.Requirement.DegreeEvidence,
            input.DegreeEvidenceAttached,
            "Consta documento de la Obediencia de procedencia que acredita el grado masónico.",
            "La incorporación requiere antecedente legalizado que acredite el grado masónico."));

        if (input.HasPeaceAndFriendshipPact is null)
        {
            requirements.Add(Observed(
                AdmissionCodes.Requirement.PeaceAndFriendshipPact,
                "Debe registrarse si la Obediencia de origen mantiene Pacto de Paz y Amistad con la GLMCh."));
            return;
        }

        requirements.Add(Approved(
            AdmissionCodes.Requirement.PeaceAndFriendshipPact,
            input.HasPeaceAndFriendshipPact.Value
                ? "Se registró que existe Pacto de Paz y Amistad con la Obediencia de origen."
                : "Se registró que no existe Pacto de Paz y Amistad con la Obediencia de origen."));

        if (!input.HasPeaceAndFriendshipPact.Value)
        {
            requirements.Add(EvaluateRequiredBoolean(
                AdmissionCodes.Requirement.GrandMasterSpecialAcceptance,
                input.GrandMasterSpecialAcceptanceApproved,
                "Gran Maestría registró la aceptación especial de la incorporación.",
                "Cuando no existe Pacto de Paz y Amistad, la aceptación de la incorporación debe ser resuelta por Gran Maestría."));
        }
    }

    private static void AddRePresentationRequirementIfNeeded(
        AdmissionEligibilityInput input,
        ICollection<AdmissionRequirementResult> requirements)
    {
        var anyRePresentationData = input.PreviousRejectionDate is not null ||
                                    input.NewPresentationDate is not null ||
                                    input.RejectionCausesRemedied is not null;
        if (!anyRePresentationData) return;

        if (input.PreviousRejectionDate is null || input.NewPresentationDate is null || input.RejectionCausesRemedied is null)
        {
            requirements.Add(Observed(
                AdmissionCodes.Requirement.RePresentation,
                "Una nueva presentación debe registrar fecha del rechazo anterior, fecha de la nueva presentación y constancia de subsanación de sus causas."));
            return;
        }

        var decision = CandidateIntakeWorkflowPolicy.EvaluateRePresentation(
            input.PreviousRejectionDate.Value,
            input.NewPresentationDate.Value,
            input.RejectionCausesRemedied.Value);

        requirements.Add(new AdmissionRequirementResult(
            AdmissionCodes.Requirement.RePresentation,
            decision.CanProceed
                ? CeremonyCodes.ValidationStatus.Approved
                : decision.IsRejected
                    ? CeremonyCodes.ValidationStatus.Rejected
                    : CeremonyCodes.ValidationStatus.Observed,
            decision.Reason));
    }

    private static AdmissionRequirementResult EvaluateRequiredBoolean(
        string code,
        bool complies,
        string approvedReason,
        string rejectedReason)
        => complies ? Approved(code, approvedReason) : Rejected(code, rejectedReason);

    private static AdmissionRequirementResult Approved(string code, string reason)
        => new(code, CeremonyCodes.ValidationStatus.Approved, reason);

    private static AdmissionRequirementResult Observed(string code, string reason)
        => new(code, CeremonyCodes.ValidationStatus.Observed, reason);

    private static AdmissionRequirementResult Rejected(string code, string reason)
        => new(code, CeremonyCodes.ValidationStatus.Rejected, reason);
}
