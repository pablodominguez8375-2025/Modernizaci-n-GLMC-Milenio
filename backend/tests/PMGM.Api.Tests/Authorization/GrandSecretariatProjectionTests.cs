using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.InstitutionalProjections;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class GrandSecretariatProjectionTests
{
    [Fact]
    public void CeremonyQueue_DoesNotExposeMemberCandidateOrNotes()
    {
        var properties = typeof(GrandSecretariatCeremonyQueueItemDto).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.DoesNotContain("MemberId", properties);
        Assert.DoesNotContain("CandidatePersonId", properties);
        Assert.DoesNotContain("Notes", properties);
        Assert.Contains("FormalAuthorizationIssued", properties);
    }

    [Fact]
    public void ReservationProjection_OmitsRestrictedNotes()
    {
        var properties = typeof(GrandSecretariatReservationDto).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.DoesNotContain("Notes", properties);
        Assert.Equal(9, properties.Count);
    }
}
