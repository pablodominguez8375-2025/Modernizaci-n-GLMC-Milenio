using Microsoft.AspNetCore.Http;
using PMGM.Api.Modules.CandidateIntake;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class CandidatePublishedLockMiddlewareTests
{
    [Theory]
    [InlineData("PUT", "/api/insinuados/solicitudes/11111111-2222-4333-8444-555555555555/ficha")]
    [InlineData("POST", "/api/insinuados/solicitudes/11111111-2222-4333-8444-555555555555/foto")]
    public void RecognizesCandidateMutationsThatMustBeLockedAfterPublication(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        var matched = CandidatePublishedLockMiddleware.TryGetCandidateMutationRequestId(context.Request, out var requestId);

        Assert.True(matched);
        Assert.Equal(Guid.Parse("11111111-2222-4333-8444-555555555555"), requestId);
    }

    [Theory]
    [InlineData("GET", "/api/insinuados/solicitudes/11111111-2222-4333-8444-555555555555/ficha")]
    [InlineData("POST", "/api/ceremonias/solicitudes/11111111-2222-4333-8444-555555555555/aprobar-publicacion-insinuado")]
    [InlineData("PUT", "/api/insinuados/solicitudes/no-es-guid/ficha")]
    public void IgnoresRequestsOutsideTheProtectedCandidateMutationSurface(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        var matched = CandidatePublishedLockMiddleware.TryGetCandidateMutationRequestId(context.Request, out _);

        Assert.False(matched);
    }
}
