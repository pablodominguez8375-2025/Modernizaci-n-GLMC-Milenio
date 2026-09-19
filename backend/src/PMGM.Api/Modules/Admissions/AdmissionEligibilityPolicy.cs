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

    public static class AffiliationProcedure
    {
        public const string Standard = "standard";
        public const string Reentry = "reentry";
        public const string Transfer = "transfer";

        public static bool IsValid(string? value)
            => value is Standard or Reentry or Transfer;
    }

    public static class Requirement
    {
        public const string AffiliationMode = "affiliation_mode";
        public const string AffiliationProcedure = "affiliation_procedure";
        public const string Article23Clearance = "article_2_3_clearance";
        public const string FirstDegreePresentation = "first_degree_presentation";
        public const string WithdrawalLetter = "withdrawal_letter";
        public const string WithdrawalLetterHandwrittenSignature = "withdrawal_letter_handwritten_signature";
        public const string InformationCommission = "information_commission";
        public const string InformationCommissionCompleted = "information_commission_completed";
        public const string LodgeThirdDegreeApproval = "lodge_third_degree_approval";
        public const string LodgeFirstDegreeBallot = "lodge_first_degree_ballot";
        public const string LegalizedInitiationEvidence = "legalized_initiation_evidence";
        public const string LegalizedWageIncreaseEvidence = "legalized_wage_increase_evidence";
        public const string LegalizedExaltationEvidence = "legalized_exaltation_evidence";
        public const string DegreeEvidence = "degree_evidence";
        public const string ObedienceRegularityRecognition = "obedience_regularity_recognition";
        public const string PeaceAndFriendshipPact = "peace_and_friendship_pact";
        public const string GrandMasterSpecialAcceptance = "grand_master_special_acceptance";
        public const string RePresentation = "re_presentation";
    }
}

public static class AdmissionProcedureRules
{
    public static bool RequiresInformationCommission(string admissionType, string? affiliationProcedure)
        => admissionType == CeremonyCodes.Type.Incorporation ||
           (admissionType == CeremonyCodes.Type.Affiliation &&
            affiliationProcedure is AdmissionCodes.AffiliationProcedure.Reentry or AdmissionCodes.AffiliationProcedure.Transfer);

    public static bool AllowsInformationCommissionWaiver(string admissionType, string? affiliationProcedure)
        => admissionType == CeremonyCodes.Type.Affiliation &&
           affiliationProcedure == AdmissionCodes.AffiliationProcedure.Transfer;
}

public sealed record AdmissionEligibilityInput(
    string AdmissionType,
    string? AffiliationMode,
    bool WithdrawalLetterAttached,
    bool WithdrawalLetterHandwrittenSignatureVerified,
    string? AffiliationProcedure = null,
    bool? Article23Clear = null,
    bool GrandMasterPardonApproved = false,
    bool FirstDegreePresentationRecorded = false,
    bool InformationCommissionRequired = false,
    bool InformationCommissionWaived = false,
    bool InformationCommissionAppointed = false,
    bool InformationCommissionCompleted = false,
    bool? LodgeThirdDegreeApproved = null,
    bool? LodgeFirstDegreeBallotApproved = null,
    bool LegalizedInitiationEvidenceAttached = false,
    bool WageIncreaseEvidenceApplies = false,
    bool LegalizedWageIncreaseEvidenceAttached = false,
    bool ExaltationEvidenceApplies = false,
    bool LegalizedExaltationEvidenceAttached = false,
    bool DegreeEvidenceAttached = false,
    bool? OriginObedienceRecognizedAsRegular = null,
    bool GrandMasterRegularityRecognitionApproved = false,
    bool? HasPeaceAndFriendshipPact = null,
    bool GrandMasterSpecialAcceptanceApproved = false,
    DateOnly? PreviousRejectionDate = null,
    DateOnly? NewPresentationDate = null,
    bool? RejectionCausesRemedied = null);

public sealed record AdmissionRequirementResult(string Code, string Status, string Reason);

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
        {
            requirements.Add(EvaluateAffiliationMode(input.AffiliationMode));
            requirements.Add(EvaluateAffiliationProcedure(input.AffiliationProcedure));
        }

        requirements.Add(EvaluateArticle23(input.Article23Clear, input.GrandMasterPardonApproved));

        requirements.Add(EvaluateRequiredBoolean(
            AdmissionCodes.Requirement.FirstDegreePresentation,
            input.FirstDegreePresentationRecorded,
            "La solicitud fue presentada y leída en Cámara de Primer Grado.",
            "El art. 2.4 exige presentar y leer la solicitud en Cámara de Primer Grado antes de continuar."));

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

        if (input.InformationCommissionRequired)
        {
            if (input.InformationCommissionWaived &&
                AdmissionProcedureRules.AllowsInformationCommissionWaiver(input.AdmissionType, input.AffiliationProcedure))
            {
                requirements.Add(Approved(
                    AdmissionCodes.Requirement.InformationCommission,
                    "La Cámara del Medio dispensó expresamente la comisión de información por tratarse de una afiliación con traslado, conforme al art. 2.5."));
                requirements.Add(Approved(
                    AdmissionCodes.Requirement.InformationCommissionCompleted,
                    "La comisión no corresponde por existir dispensa válida de la Cámara del Medio para el traslado."));
            }
            else
            {
                requirements.Add(EvaluateRequiredBoolean(
                    AdmissionCodes.Requirement.InformationCommission,
                    input.InformationCommissionAppointed,
                    "Consta la comisión de información integrada por tres Maestros.",
                    "El art. 2.5 exige una comisión de tres Maestros para este expediente."));

                requirements.Add(EvaluateRequiredBoolean(
                    AdmissionCodes.Requirement.InformationCommissionCompleted,
                    input.InformationCommissionCompleted,
                    "La comisión de información dejó constancia de haber concluido su encargo.",
                    "La comisión de información debe completar su encargo antes de resolver la tramitación."));
            }
        }

        requirements.Add(EvaluateDecision(
            AdmissionCodes.Requirement.LodgeThirdDegreeApproval,
            input.LodgeThirdDegreeApproved,
            "La aprobación de 3.er grado por al menos dos tercios de los Maestros presentes está registrada.",
            "La decisión de 3.er grado aún no ha sido registrada.",
            "La votación de 3.er grado no alcanzó los dos tercios de los Maestros presentes."));

        requirements.Add(EvaluateDecision(
            AdmissionCodes.Requirement.LodgeFirstDegreeBallot,
            input.LodgeFirstDegreeBallotApproved,
            "El balotaje secreto de 1.er grado está aprobado y registrado con el procedimiento de Iniciación.",
            "El balotaje secreto de 1.er grado aún no ha sido registrado.",
            "El balotaje de 1.er grado registrado no es aprobatorio."));

        if (input.AdmissionType == CeremonyCodes.Type.Affiliation)
            AddRePresentationRequirementIfNeeded(input, requirements);
        else
            AddIncorporationRequirements(input, requirements);

        var rejected = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Rejected);
        var observed = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Observed);
        var status = rejected ? "does_not_comply" : observed ? "observed" : "complies";

        return new AdmissionEligibilityDecision(status == "complies", status, requirements);
    }

    public static CandidateWorkflowDecision EvaluateThirdDegreeVote(
        int presentMasters,
        int votesInFavor,
        int votesAgainst,
        int abstentions)
    {
        if (presentMasters <= 0)
            return CandidateWorkflowDecision.Blocked("admission.third_degree.quorum", "Debe registrarse al menos un Maestro presente.");
        if (votesInFavor < 0 || votesAgainst < 0 || abstentions < 0 ||
            votesInFavor + votesAgainst + abstentions != presentMasters)
            return CandidateWorkflowDecision.Blocked("admission.third_degree.counts", "La suma de votos favorables, desfavorables y abstenciones debe coincidir con los Maestros presentes.");

        return votesInFavor * 3 >= presentMasters * 2
            ? CandidateWorkflowDecision.Allowed("admission.third_degree.approved", "La tramitación fue aprobada por al menos dos tercios de los Maestros presentes.")
            : CandidateWorkflowDecision.Rejected("admission.third_degree.rejected", "La tramitación no alcanzó los dos tercios de los Maestros presentes exigidos por el art. 2.4.");
    }

    public static CandidateWorkflowDecision EvaluateFirstDegreeBallot(
        IReadOnlyCollection<CandidateBallotRound> rounds,
        bool approved)
        => CandidateIntakeWorkflowPolicy.EvaluateFinalBallotRounds(rounds, approved);

    private static AdmissionRequirementResult EvaluateArticle23(bool? clear, bool pardon)
    {
        if (clear == true)
            return Approved(AdmissionCodes.Requirement.Article23Clearance, "Régimen Interior confirmó que no existe impedimento del art. 2.3.");
        if (clear == false && pardon)
            return Approved(AdmissionCodes.Requirement.Article23Clearance, "Existe antecedente del art. 2.3, pero consta indulto habilitante de Gran Maestría.");
        if (clear == false)
            return Rejected(AdmissionCodes.Requirement.Article23Clearance, "Existe pena de rayamiento o sentencia de retiro forzoso impuesta por Tribunal sin indulto habilitante.");
        return Observed(AdmissionCodes.Requirement.Article23Clearance, "Régimen Interior debe revisar el impedimento del art. 2.3 antes de tramitar la afiliación/incorporación.");
    }

    private static AdmissionRequirementResult EvaluateAffiliationMode(string? mode)
        => AdmissionCodes.AffiliationMode.IsValid(mode)
            ? Approved(AdmissionCodes.Requirement.AffiliationMode, "Se indicó la modalidad de afiliación simple o con activación.")
            : Rejected(AdmissionCodes.Requirement.AffiliationMode, "Debe indicarse si la afiliación es simple o con activación.");

    private static AdmissionRequirementResult EvaluateAffiliationProcedure(string? procedure)
        => AdmissionCodes.AffiliationProcedure.IsValid(procedure)
            ? Approved(AdmissionCodes.Requirement.AffiliationProcedure, "Se clasificó expresamente el procedimiento como estándar, reintegro o traslado.")
            : Rejected(AdmissionCodes.Requirement.AffiliationProcedure, "Debe clasificarse expresamente el procedimiento de afiliación como estándar, reintegro o traslado; no se infiere desde simple/con activación.");

    private static AdmissionRequirementResult EvaluateDecision(
        string code,
        bool? approved,
        string approvedReason,
        string pendingReason,
        string rejectedReason)
        => approved switch
        {
            true => Approved(code, approvedReason),
            false => Rejected(code, rejectedReason),
            null => Observed(code, pendingReason)
        };

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
            requirements.Add(EvaluateRequiredBoolean(
                AdmissionCodes.Requirement.LegalizedWageIncreaseEvidence,
                input.LegalizedWageIncreaseEvidenceAttached,
                "Consta antecedente legalizado de aumento de salario.",
                "Debe adjuntarse el antecedente legalizado de aumento de salario cuando corresponda."));

        if (input.ExaltationEvidenceApplies)
            requirements.Add(EvaluateRequiredBoolean(
                AdmissionCodes.Requirement.LegalizedExaltationEvidence,
                input.LegalizedExaltationEvidenceAttached,
                "Consta antecedente legalizado de exaltación.",
                "Debe adjuntarse el antecedente legalizado de exaltación cuando corresponda."));

        requirements.Add(EvaluateRequiredBoolean(
            AdmissionCodes.Requirement.DegreeEvidence,
            input.DegreeEvidenceAttached,
            "Consta documento de la Obediencia de procedencia que acredita el grado masónico.",
            "La incorporación requiere antecedente legalizado que acredite el grado masónico."));

        if (input.OriginObedienceRecognizedAsRegular is null)
        {
            requirements.Add(Observed(
                AdmissionCodes.Requirement.ObedienceRegularityRecognition,
                "Debe registrarse si la Obediencia de origen es reconocida como regular."));
        }
        else if (input.OriginObedienceRecognizedAsRegular.Value)
        {
            requirements.Add(Approved(
                AdmissionCodes.Requirement.ObedienceRegularityRecognition,
                "La Obediencia de origen está registrada como reconocida regular."));
        }
        else
        {
            requirements.Add(EvaluateRequiredBoolean(
                AdmissionCodes.Requirement.ObedienceRegularityRecognition,
                input.GrandMasterRegularityRecognitionApproved,
                "Gran Maestría reconoció expresamente la calidad masónica regular o autorizó la regularización.",
                "El art. 2.1 exige reconocimiento expreso o autorización de regularización por Gran Maestría cuando la Obediencia no es reconocida regular."));
        }

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
            requirements.Add(EvaluateRequiredBoolean(
                AdmissionCodes.Requirement.GrandMasterSpecialAcceptance,
                input.GrandMasterSpecialAcceptanceApproved,
                "Gran Maestría registró la aceptación especial de la incorporación.",
                "Cuando no existe Pacto de Paz y Amistad, la aceptación de la incorporación debe ser resuelta por Gran Maestría."));
    }

    private static void AddRePresentationRequirementIfNeeded(
        AdmissionEligibilityInput input,
        ICollection<AdmissionRequirementResult> requirements)
    {
        var hasRePresentationContext = input.PreviousRejectionDate is not null ||
                                       input.RejectionCausesRemedied is not null;
        if (!hasRePresentationContext) return;

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
        string code, bool complies, string approvedReason, string rejectedReason)
        => complies ? Approved(code, approvedReason) : Rejected(code, rejectedReason);

    private static AdmissionRequirementResult Approved(string code, string reason)
        => new(code, CeremonyCodes.ValidationStatus.Approved, reason);

    private static AdmissionRequirementResult Observed(string code, string reason)
        => new(code, CeremonyCodes.ValidationStatus.Observed, reason);

    private static AdmissionRequirementResult Rejected(string code, string reason)
        => new(code, CeremonyCodes.ValidationStatus.Rejected, reason);
}
