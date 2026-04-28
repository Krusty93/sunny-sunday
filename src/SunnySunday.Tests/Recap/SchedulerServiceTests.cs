using SunnySunday.Server.Models;
using SunnySunday.Server.Services;

namespace SunnySunday.Tests.Recap;

public sealed class SchedulerServiceTests
{
    [Theory]
    [InlineData("daily", null, "18:00", "0 00 18 * * ?")]
    [InlineData("daily", null, "09:30", "0 30 09 * * ?")]
    [InlineData("daily", null, "00:00", "0 00 00 * * ?")]
    [InlineData("weekly", "monday", "18:00", "0 00 18 ? * MON")]
    [InlineData("weekly", "friday", "07:15", "0 15 07 ? * FRI")]
    [InlineData("weekly", "sunday", "20:00", "0 00 20 ? * SUN")]
    public void BuildCronExpression_ProducesExpectedCron(string schedule, string? deliveryDay, string deliveryTime, string expectedCron)
    {
        var settings = new Settings
        {
            Schedule = schedule,
            DeliveryDay = deliveryDay,
            DeliveryTime = deliveryTime,
            Timezone = "UTC"
        };

        var cron = SchedulerService.BuildCronExpression(settings);

        Assert.Equal(expectedCron, cron);
    }

    [Fact]
    public void BuildCronExpression_WeeklyWithoutDeliveryDay_ProducesDailyPattern()
    {
        var settings = new Settings
        {
            Schedule = "weekly",
            DeliveryDay = null,
            DeliveryTime = "18:00",
            Timezone = "UTC"
        };

        var cron = SchedulerService.BuildCronExpression(settings);

        // Without a delivery day, falls back to daily-like pattern
        Assert.Equal("0 00 18 * * ?", cron);
    }
}
