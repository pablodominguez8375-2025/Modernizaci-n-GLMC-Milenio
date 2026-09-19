namespace PMGM.Api.Modules.Admissions;

public static class AdmissionWorkflowCodes
{
    public static class CaseStatus
    {
        public const string UnderReview = "under_review";
        public const string Eligible = "eligible";
        public const string Observed = "observed";
        public const string Rejected = "rejected";
        public const string Resolved = "resolved";
    }

    public static class EvidenceType
    {
        public const string WithdrawalLetter = "withdrawal_letter";
        public const string LegalizedInitiation = "legalized_initiation_evidence";
        public const string LegalizedWageIncrease = "legalized_wage_increase_evidence";
        public const string LegalizedExaltation = "legalized_exaltation_evidence";
        public const string Degree = "degree_evidence";
        public const string InformationCommissionReport = "information_commission_report";

        public static bool IsValid(string value)
            => value is WithdrawalLetter or LegalizedInitiation or LegalizedWageIncrease or LegalizedExaltation or Degree or InformationCommissionReport;
    }

    public static class DecisionType
    {
        public const string WithdrawalLetterHandwrittenSignature = "withdrawal_letter_handwritten_signature";
        public const string Article23Review = "article_2_3_review";
        public const string GrandMasterPardon = "grand_master_pardon";
        public const string LodgeFirstDegreePresentation = "lodge_first_degree_presentation";
        public const string InformationCommissionCompleted = "information_commission_completed";
        public const string GrandMasterRegularityRecognition = "grand_master_regularity_recognition";
        public const string LodgeThirdDegreeApproval = "lodge_third_degree_approval";
        public const string LodgeFirstDegreeBallot = "lodge_first_degree_ballot";
        public const string GrandMasterSpecialAcceptance = "grand_master_special_acceptance";
        public const string CeremonyRequestCreated = "ceremony_request_created";
        public const string CeremonyCompleted = "ceremony_completed";
        public const string EvidenceReviewPrefix = "evidence_review:";

        public static string EvidenceReview(Guid evidenceId) => $"{EvidenceReviewPrefix}{evidenceId:D}";
    }
}
