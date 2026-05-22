using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfBet.Api.Configuration;
using SelfBet.Api.Endpoints;
using SelfBet.Api.Workers;
using SelfBet.Application;
using SelfBet.Automation;
using SelfBet.Infrastructure;
using SelfBet.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(opt =>
{
    opt.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection("Scheduler"));
builder.Services.AddSelfBetApplication();
builder.Services.AddSelfBetInfrastructure(builder.Configuration);
builder.Services.AddSelfBetAutomation(builder.Configuration);
builder.Services.AddHostedService<TwiceDailyRunWorker>();
builder.Services.AddHostedService<TeamStrengthRefreshWorker>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ── Auto-migrate on startup ───────────────────────────────────────────────
{
    const int maxAttempts = 6;
    var startupLogger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupMigration");

    Exception? last = null;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SelfBetDbContext>();
            await StartupDatabaseMigration.ApplyAsync(db, startupLogger);
            last = null;
            break;
        }
        catch (Exception ex)
        {
            last = ex;
            if (attempt >= maxAttempts)
            {
                break;
            }

            var delay = TimeSpan.FromSeconds(Math.Min(30, attempt * 5));
            startupLogger.LogWarning(
                ex,
                "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds}s.",
                attempt,
                maxAttempts,
                delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }

    if (last is not null)
    {
        startupLogger.LogError(last, "Database migration failed after {MaxAttempts} attempts.", maxAttempts);
        throw last;
    }
}

app.UseCors();

app.MapGet("/health/live",  () => Results.Ok(new { status = "live",  utc = DateTimeOffset.UtcNow }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", utc = DateTimeOffset.UtcNow }));

app.MapAutomationEndpoints();
app.MapRunEndpoints();
app.MapSlipEndpoints();
app.MapStrategyConfigEndpoints();
app.MapBankrollEndpoints();
app.MapPerformanceEndpoints();
app.MapAuditEndpoints();
app.MapPredictionEndpoints();

app.Run();
