using PMGM.Api.Modules.CandidateIntake;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidateInterviewReschedulePolicyTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);

    [Fact]
    public void FutureInterviewWithReasonMayBeRescheduled()
        => Assert.Null(CandidateInterviewReschedulePolicy.Validate(
            Today.AddDays(7), Today, "Entrevistador solicita ajuste de agenda"));

    [Fact]
    public void PastOrUnreasonablyFutureDatesAreRejected()
    {
        Assert.NotNull(CandidateInterviewReschedulePolicy.Validate(Today.AddDays(-1), Today, "Motivo válido para nueva fecha"));
        Assert.NotNull(CandidateInterviewReschedulePolicy.Validate(Today.AddYears(3), Today, "Motivo válido para nueva fecha"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("corto")]
    public void MissingOrTooShortReasonIsRejected(string reason)
        => Assert.NotNull(CandidateInterviewReschedulePolicy.Validate(Today.AddDays(1), Today, reason));

    [Fact]
    public void ReasonMustFitAuditBound()
        => Assert.NotNull(CandidateInterviewReschedulePolicy.Validate(
            Today.AddDays(1), Today, new string('X',1001)));
}
