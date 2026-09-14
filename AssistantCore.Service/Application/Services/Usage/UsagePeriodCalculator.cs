namespace AssistantCore.Service.Application.Services.Usage;

internal static class UsagePeriodCalculator
{
    public static (DateTimeOffset Start, DateTimeOffset End) ComputeMonthlyPeriod(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }
}
