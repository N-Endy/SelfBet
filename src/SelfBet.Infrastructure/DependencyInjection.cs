using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SelfBet.Application.Abstractions;
using SelfBet.Infrastructure.Persistence;
using SelfBet.Infrastructure.Providers;
using SelfBet.Infrastructure.Services;

namespace SelfBet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSelfBetInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Postgres via EF Core ──────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not set. " +
                "Set it via the CONNECTIONSTRINGS__DEFAULTCONNECTION environment variable.");

        services.AddDbContext<SelfBetDbContext>(opts =>
            opts.UseNpgsql(connectionString,
                npgsql => npgsql.EnableRetryOnFailure(3)));

        // ── Repositories ─────────────────────────────────────────────────────
        services.AddScoped<IStrategyConfigRepository, EfStrategyConfigRepository>();
        services.AddScoped<IBankrollRepository, EfBankrollRepository>();
        services.AddScoped<ISlipRepository, EfSlipRepository>();
        services.AddScoped<IRunRepository, EfRunRepository>();
        services.AddScoped<IPlacementRepository, EfPlacementRepository>();
        services.AddScoped<IAuditService, DbAuditService>();

        // ── Calibration ───────────────────────────────────────────────────────
        services.AddScoped<ICalibrationService, CalibrationService>();

        // ── Football data provider (SportyBet live) ───────────────────────────
        services.AddMemoryCache();
        services.AddHttpClient("SportyBetData")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        services.AddScoped<IFootballDataProvider, SportyBetMarketDataProvider>(sp =>
            new SportyBetMarketDataProvider(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SportyBetMarketDataProvider>>()));

        // ── API-Football historical-match provider ───────────────────────────
        services.Configure<ApiFootballOptions>(configuration.GetSection("ApiFootball"));
        services.AddHttpClient<IHistoricalMatchProvider, ApiFootballHistoricalMatchProvider>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ApiFootballOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ── Team strength model (Dixon-Coles fitted from historical data) ───
        services.AddScoped<IHistoricalMatchRepository, EfHistoricalMatchRepository>();
        services.AddScoped<ITeamStrengthRepository, EfTeamStrengthRepository>();
        services.AddScoped<ITeamStrengthService, TeamStrengthService>();

        // ── SMTP Email ────────────────────────────────────────────────────────
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.Section));
        services.AddScoped<IEmailNotifier, SmtpEmailNotifier>();

        return services;
    }
}
