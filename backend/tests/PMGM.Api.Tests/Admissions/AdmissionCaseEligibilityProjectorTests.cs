using System.Text.Json;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Admissions;

public sealed class AdmissionCaseEligibilityProjectorTests
{
    [Fact]
    public void Transfer_does_not_accept_commission_waiver_recorded_after_third_degree()
    {
        var admissionCase = BuildTransferCase();
        var thirdDegree = admissionCase.Decisions.Single(x => x.DecisionType == AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval);

        admissionCase.Decisions.Add(new AdmissionDecision
        {
            AdmissionCaseId = admissionCase.Id,
            DecisionType = AdmissionWorkflowCodes.DecisionType.InformationCommissionWaiver,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = thirdDegree.AsOfDate.AddDays(1),
            SourceReference = "ACTA-CAMARA-MEDIO-POSTERIOR",
            StructuredDataJson = JsonSerializer.Serialize(new { waiver = true, basis = "article_2_5_transfer", authority = "camara_del_medio" }),
            RecordedBySubject = "secretaria",
            RecordedAtUtc = thirdDegree.RecordedAtUtc.AddHours(1)
        });

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);

        Assert.False(projection.Decision.CanProceed);
        Assert.Contains(projection.Decision.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.InformationCommission &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
    }

    [Fact]
    public void Transfer_does_not_accept_commission_completion_recorded_after_third_degree()
    {
        var admissionCase = BuildTransferCase();
        var thirdDegree = admissionCase.Decisions.Single(x => x.DecisionType == AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval);
        var groupId = Guid.NewGuid();
        var appointmentDate = thirdDegree.AsOfDate.AddDays(-2);
        var appointmentRecordedAt = thirdDegree.RecordedAtUtc.AddDays(-2);

        foreach (var memberId in new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() })
        {
            admissionCase.CommissionAppointments.Add(new AdmissionCommissionAppointment
            {
                AdmissionCaseId = admissionCase.Id,
                AppointmentGroupId = groupId,
                MemberId = memberId,
                AppointmentDate = appointmentDate,
                SourceReference = "ACTA-NOMBRAMIENTO",
                AppointedBySubject = "venerable",
                RecordedAtUtc = appointmentRecordedAt
            });
        }

        admissionCase.Decisions.Add(new AdmissionDecision
        {
            AdmissionCaseId = admissionCase.Id,
            DecisionType = AdmissionWorkflowCodes.DecisionType.InformationCommissionCompleted,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = thirdDegree.AsOfDate.AddDays(1),
            SourceReference = "INFORME-COMISION-POSTERIOR",
            StructuredDataJson = JsonSerializer.Serialize(new { completed = true, appointmentGroupId = groupId }),
            RecordedBySubject = "secretaria",
            RecordedAtUtc = thirdDegree.RecordedAtUtc.AddHours(1)
        });

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);

        Assert.False(projection.Decision.CanProceed);
        Assert.Contains(projection.Decision.Requirements, x =>
            x.Code == AdmissionCodes.Requirement.InformationCommissionCompleted &&
            x.Status == CeremonyCodes.ValidationStatus.Rejected);
    }

    private static AdmissionCase BuildTransferCase()
    {
        var caseId = Guid.NewGuid();
        var recordedBase = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var presentationDate = new DateOnly(2026, 9, 10);
        var thirdDegreeDate = new DateOnly(2026, 9, 15);
        var ballotDate = new DateOnly(2026, 9, 16);

        var admissionCase = new AdmissionCase
        {
            Id = caseId,
            OrganizationId = Guid.NewGuid(),
            AdmissionType = CeremonyCodes.Type.Affiliation,
            AffiliationMode = AdmissionCodes.AffiliationMode.Simple,
            AffiliationProcedure = AdmissionCodes.AffiliationProcedure.Transfer,
            MemberId = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            Status = AdmissionWorkflowCodes.CaseStatus.UnderReview,
            CreatedBySubject = "secretaria",
            CreatedAtUtc = recordedBase.AddDays(-3)
        };

        admissionCase.Evidence.Add(new AdmissionEvidence
        {
            AdmissionCaseId = caseId,
            EvidenceType = AdmissionWorkflowCodes.EvidenceType.WithdrawalLetter,
            SourceReference = "CRV-001",
            ReviewStatus = CeremonyCodes.ValidationStatus.Approved,
            CreatedBySubject = "secretaria",
            CreatedAtUtc = recordedBase.AddDays(-2)
        });

        admissionCase.Decisions.Add(new AdmissionDecision
        {
            AdmissionCaseId = caseId,
            DecisionType = AdmissionWorkflowCodes.DecisionType.WithdrawalLetterHandwrittenSignature,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = presentationDate.AddDays(-2),
            SourceReference = "VERIFICACION-CRV",
            RecordedBySubject = "secretaria",
            RecordedAtUtc = recordedBase.AddDays(-2)
        });
        admissionCase.Decisions.Add(new AdmissionDecision
        {
            AdmissionCaseId = caseId,
            DecisionType = AdmissionWorkflowCodes.DecisionType.Article23Review,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = presentationDate.AddDays(-1),
            SourceReference = "RI-2.3",
            RecordedBySubject = "regimen",
            RecordedAtUtc = recordedBase.AddDays(-1)
        });
        admissionCase.Decisions.Add(new AdmissionDecision
        {
            AdmissionCaseId = caseId,
            DecisionType = AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreePresentation,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = presentationDate,
            SourceReference = "ACTA-1G",
            RecordedBySubject = "secretaria",
            RecordedAtUtc = recordedBase
        });
        admissionCase.Decisions.Add(new AdmissionDecision
        {
            AdmissionCaseId = caseId,
            DecisionType = AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = thirdDegreeDate,
            SourceReference = "ACTA-3G",
            RecordedBySubject = "secretaria",
            RecordedAtUtc = recordedBase.AddDays(5)
        });
        admissionCase.Decisions.Add(new AdmissionDecision
        {
            AdmissionCaseId = caseId,
            DecisionType = AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreeBallot,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = ballotDate,
            SourceReference = "ACTA-BALOTAJE",
            RecordedBySubject = "secretaria",
            RecordedAtUtc = recordedBase.AddDays(6)
        });

        return admissionCase;
    }
}
