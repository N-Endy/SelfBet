using Microsoft.Extensions.Options;
using SelfBet.Api.Configuration;
using SelfBet.Application.UseCases;

namespace SelfBet.Api.Workers;

public sealed class TwiceDailyRunWorker(
    ILogger<TwiceDailyRunWorker> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<SchedulerOptions> options) : BackgroundService
{
    private readonly SchedulerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("TwiceDailyRunWorker disabled.");
            return;
        }

        var tz = ResolveTimeZone(_options.TimeZone);
        logger.LogInformation("TwiceDailyRunWorker started. tz={TimeZone} times={Times}", tz.Id, string.Join(", ", _options.DailyRunTimesLocal));

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextUtc = NextRunUtc(tz);
            var delay = nextUtc - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("Next scheduled run at {NextUtc} (UTC)", nextUtc);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }

            await ExecuteRunAsync(stoppingToken);

            try
            {
                await Task.Delay(_options.PostRunCooldown, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task ExecuteRunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<RunEngine>();
            var outcome = await engine.ExecuteAsync("scheduler", cancellationToken);
            logger.LogInformation("Scheduled run {RunId} completed with status {Status}", outcome.Run.Id, outcome.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled run failed");
        }
    }

    private DateTimeOffset NextRunUtc(TimeZoneInfo tz)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);
        DateTimeOffset? candidate = null;

        foreach (var slot in _options.DailyRunTimesLocal.OrderBy(t => t))
        {
            var slotLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, slot.Hour, slot.Minute, 0, DateTimeKind.Unspecified);
            var slotUtc = new DateTimeOffset(slotLocal, tz.GetUtcOffset(slotLocal)).ToUniversalTime();
            if (slotUtc > nowUtc)
            {
                candidate = slotUtc;
                break;
            }
        }

        if (candidate is null)
        {
            var first = _options.DailyRunTimesLocal.OrderBy(t => t).First();
            var tomorrowLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, first.Hour, first.Minute, 0, DateTimeKind.Unspecified).AddDays(1);
            candidate = new DateTimeOffset(tomorrowLocal, tz.GetUtcOffset(tomorrowLocal)).ToUniversalTime();
        }

        return candidate.Value;
    }

    private TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            logger.LogWarning("Time zone {Tz} not found, defaulting to UTC.", id);
            return TimeZoneInfo.Utc;
        }
    }
}
