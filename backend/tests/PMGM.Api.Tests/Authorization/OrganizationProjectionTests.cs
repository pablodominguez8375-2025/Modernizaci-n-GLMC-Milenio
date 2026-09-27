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

        Assert.Equal(new[] { "City", "Country", "EstablishedOn" }, properties);
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
}
