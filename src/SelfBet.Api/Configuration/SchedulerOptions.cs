namespace SelfBet.Api.Configuration;

public sealed class SchedulerOptions
{
    public bool Enabled { get; init; } = true;
    public string TimeZone { get; init; } = "Africa/Lagos";
    public List<TimeOnly> DailyRunTimesLocal { get; init; } = [new(8, 0), new(16, 0)];
    public TimeSpan PostRunCooldown { get; init; } = TimeSpan.FromMinutes(2);
}
