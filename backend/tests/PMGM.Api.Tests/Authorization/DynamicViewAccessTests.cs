using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using Xunit;
namespace PMGM.Api.Tests.Authorization;
public sealed class DynamicViewAccessTests
{
    [Fact]
    public void Historical_enrollment_denies_every_ungranted_view_and_never_falls_back()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog(); var org = Guid.NewGuid(); var today = new DateOnly(2026,10,7);
        catalog.Profiles.Add(new(Guid.NewGuid(), "read", "Read", "lodge", false, true, ["lodge"], [new("lodge", ["view"])]));
        var assignment = new DynamicAssignment(Guid.NewGuid(), "subject", "read", org, today, null, true); catalog.Assignments.Add(assignment);
        Assert.True(DynamicViewAccess.Allows(catalog,"subject","lodge","view",org,today));
        Assert.False(DynamicViewAccess.Allows(catalog,"subject","lodge","create",org,today));
        Assert.False(DynamicViewAccess.Allows(catalog,"subject","members","view",org,today));
        Assert.True(DynamicViewAccess.Allows(catalog,"legacy","members","view",org,today));
        foreach(var revoked in new[]{assignment with{IsActive=false},assignment with{EffectiveTo=today.AddDays(-1)},assignment with{EffectiveFrom=today.AddDays(1)}})
        {
            catalog.Assignments[0]=revoked; Assert.False(DynamicViewAccess.Allows(catalog,"subject","lodge","view",org,today));
        }
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","subject")],"test"));
        Assert.Equal(new Guid?[]{org},DynamicViewAccess.AggregateScopes(catalog,user));
    }
    [Theory]
    [InlineData("/api/member-self/profile","member")]
    [InlineData("/api/membership/me/hospitalaria","member")]
    [InlineData("/api/institutional/organizations/{id:guid}/profile","lodgeprofile")]
    [InlineData("/api/members/{id:guid}/history","members")]
    [InlineData("/api/gestion-logial/consejo/sesiones/{sessionId:guid}","lodge")]
    [InlineData("/api/regimen-interior/data-quality/cases","regimen")]
    [InlineData("/api/insinuados/solicitudes/{requestId:guid}/ficha","candidateprofile")]
    [InlineData("/api/insinuados/solicitudes/{requestId:guid}/flujo","initiationcircuit")]
    [InlineData("/api/admisiones/expedientes","admissions")]
    [InlineData("/api/institutional/ceremonias/bandeja","ceremonies")]
    [InlineData("/api/gran-secretaria/documentos","secretariat")]
    [InlineData("/api/biblioteca/{documentId:guid}/contenido","library")]
    [InlineData("/api/documentos/versiones/{versionId:guid}/contenido","documentmanager")]
    [InlineData("/api/grand-archive/{recordId:guid}/withdraw","grandarchive")]
    [InlineData("/api/calendar/ics","calendar")]
    [InlineData("/api/notifications/me","notifications")]
    [InlineData("/api/system/settings","system")]
    [InlineData("/api/system/audit-events","access-review")]
    public void Catalog_domains_have_explicit_view_contracts(string route,string view) => Assert.Equal(view,DynamicViewAccess.ViewFor(route));
    [Theory]
    [InlineData("GET","ExportIcsAsync","view")]
    [InlineData("POST","CreateMeetingAsync","create")]
    [InlineData("POST","RecordAttendanceAsync","write")]
    [InlineData("PUT","UploadContentAsync","write")]
    [InlineData("POST","UpdateCorrespondenceStatusAsync","edit")]
    [InlineData("POST","WithdrawAsync","delete")]
    public void Actions_are_independent(string method,string handler,string action) => Assert.Equal(action,DynamicViewAccess.ActionFor(method,"/api/test",handler));
}
