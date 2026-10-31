using GSYNC.App.Infrastructure;
using Xunit;

namespace GSYNC.AppLogic.Tests;

public sealed class NotificationAreaIconTests
{
    [Theory]
    [InlineData(0x0202u, true, (int)TrayAction.Show)]
    [InlineData(0x0400u, true, (int)TrayAction.Show)]
    [InlineData(0x0401u, true, (int)TrayAction.Show)]
    [InlineData(0x007Bu, true, (int)TrayAction.Menu)]
    [InlineData(0x0205u, true, (int)TrayAction.None)]
    [InlineData(0x0205u, false, (int)TrayAction.Menu)]
    [InlineData(0x0200u, false, (int)TrayAction.None)]
    public void TrayEventsMapToOpenOrExitMenu(uint message, bool version4, int expected)
        => Assert.Equal(expected, (int)NotificationAreaIcon.Classify(message, version4));
}
