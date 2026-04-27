using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
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
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SelfBetDbContext>();
    await db.Database.MigrateAsync();
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
