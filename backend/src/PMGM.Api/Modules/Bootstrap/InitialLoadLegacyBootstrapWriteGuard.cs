using PMGM.Api.Modules.Bootstrap.Entities;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Fails closed for legacy institutional bootstrap writes after the initial
/// enrollment window has begun. This is NOT permission to ingest real PII.
/// The caller must hold a row lock on the singleton until transaction commit.
/// </summary>
public static class InitialLoadLegacyBootstrapWriteGuard
{
    public static bool AllowsApply(InitialLoadWindow? window) =>
        window is not null &&
        InitialLoadWindowStatusReader.Project(window).Status == InitialLoadWindow.NotOpened;
}
