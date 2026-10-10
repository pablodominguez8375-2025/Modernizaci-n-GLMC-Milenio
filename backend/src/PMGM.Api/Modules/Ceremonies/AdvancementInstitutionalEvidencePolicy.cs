namespace PMGM.Api.Modules.Ceremonies;

/// <summary>Issue #47: decisión fail-closed; nunca confundir actas con acuerdos.</summary>
public static class AdvancementInstitutionalEvidencePolicy
{
    public static bool IsCertified(bool presentationRecorded, bool institutionallyApproved,
        bool evidenceStillValid, bool actorSeparation)
        => presentationRecorded && institutionallyApproved && evidenceStillValid && actorSeparation;
}
