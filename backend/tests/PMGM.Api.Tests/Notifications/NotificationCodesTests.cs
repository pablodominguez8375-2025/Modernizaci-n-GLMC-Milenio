using PMGM.Api.Modules.Notifications;
using Xunit;

namespace PMGM.Api.Tests.Notifications;

public sealed class NotificationCodesTests
{
    [Theory]
    [InlineData(NotificationCodes.Channel.Internal)]
    [InlineData(NotificationCodes.Channel.Email)]
    public void Channels_accept_supported_values(string value)
        => Assert.True(NotificationCodes.Channel.IsValid(value));

    [Theory]
    [InlineData(NotificationCodes.DeliveryStatus.Queued)]
    [InlineData(NotificationCodes.DeliveryStatus.Delivered)]
    [InlineData(NotificationCodes.DeliveryStatus.Failed)]
    [InlineData(NotificationCodes.DeliveryStatus.DeadLetter)]
    public void Delivery_status_accepts_supported_values(string value)
        => Assert.True(NotificationCodes.DeliveryStatus.IsValid(value));

    [Fact]
    public void Unknown_channel_is_rejected()
        => Assert.False(NotificationCodes.Channel.IsValid("sms"));
}
