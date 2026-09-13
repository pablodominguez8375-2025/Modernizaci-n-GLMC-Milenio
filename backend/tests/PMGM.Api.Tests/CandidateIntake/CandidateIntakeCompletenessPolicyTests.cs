using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.CandidateIntake.Entities;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidateIntakeCompletenessPolicyTests
{
    [Fact]
    public void CompleteOfficialProfile_IsEligibleForPublicationReview()
    {
        var result = CandidateIntakeCompletenessPolicy.Evaluate(CompleteProfile(), "Persona Demo", "+56 9 0000 0000", "demo@ejemplo.cl", "Dirección demo");

        Assert.True(result.IsComplete);
        Assert.Equal(100, result.Percent);
        Assert.Empty(result.MissingRequirements);
    }

    [Fact]
    public void MissingPhotoAndSecretary_AreReportedExplicitly()
    {
        var profile = CompleteProfile();
        profile.PhotoVersionId = null;
        profile.ResponsibleSecretaryName = null;

        var result = CandidateIntakeCompletenessPolicy.Evaluate(profile, "Persona Demo", "+56 9 0000 0000", "demo@ejemplo.cl", "Dirección demo");

        Assert.False(result.IsComplete);
        Assert.Contains("Fotografía tipo pasaporte", result.MissingRequirements);
        Assert.Contains("Secretario responsable", result.MissingRequirements);
    }

    private static CandidateIntakeProfile CompleteProfile() => new()
    {
        PaternalSurname = "QA",
        RutOrInstitutionalId = "DEMO-01",
        BirthDate = new DateOnly(1990, 1, 1),
        Nationality = "Chilena",
        CivilStatus = "Demo",
        City = "Santiago",
        Occupation = "Profesional",
        EmployerName = "Empresa Demo",
        WorkAddress = "Dirección laboral",
        WorkPosition = "Cargo demo",
        WorkPhone = "+56 2 2000 0000",
        Orient = "Santiago",
        PresentersJson = "[\"H∴ Presentante Demo\"]",
        InsinuationDate = new DateOnly(2026, 8, 1),
        FirstDegreePresentationDate = new DateOnly(2026, 8, 8),
        ResponsibleSecretaryName = "H∴ Secretario Demo",
        PhotoVersionId = Guid.NewGuid()
    };
}
