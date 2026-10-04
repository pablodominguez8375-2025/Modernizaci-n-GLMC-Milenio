using PMGM.Api.Modules.Core;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class OrganizationProjectionTests
{
    [Fact]
    public void WorkshopMetadataUpdateContract_ContainsOnlyApprovedFields()
    {
        var properties = typeof(UpdateOrganizationMetadataRequest)
            .GetProperties()
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "City", "Country", "EstablishedOn", "Name", "OrienteCode" }, properties);
    }

    [Fact]
    public void OrganizationOptionSurface_IsPurposeMinimized()
    {
        var properties = typeof(OrganizationOptionDto)
            .GetProperties()
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "Id", "Name", "Number", "Type" }, properties);
    }

    [Fact]
    public void OrganizationOptionsResponse_ContainsOnlyCountAndItems()
    {
        var properties = typeof(OrganizationOptionsResponse)
            .GetProperties()
            .Select(x => x.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "Items", "Total" }, properties);
    }

    [Fact]
    public void WorkshopLogoPolicy_AllowsOnlySignatureMatchedPngAndJpeg()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0x00];
        Assert.True(WorkshopLogoContentTypePolicy.IsAllowed("image/png", png));
        Assert.True(WorkshopLogoContentTypePolicy.IsAllowed("image/jpeg", jpeg));
        Assert.False(WorkshopLogoContentTypePolicy.IsAllowed("image/svg+xml", "<svg/>"u8));
        Assert.False(WorkshopLogoContentTypePolicy.IsAllowed("image/png", "<svg/>"u8));
    }
}
