using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SelfBet.Application.Abstractions;
using SelfBet.Automation.Adapters;
using SelfBet.Automation.Clients;
using SelfBet.Automation.Models;

namespace SelfBet.Automation;

public static class DependencyInjection
{
    public static IServiceCollection AddSelfBetAutomation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var opts = new SportyBetOptions();
        configuration.GetSection(SportyBetOptions.Section).Bind(opts);
        services.AddSingleton(opts);

        services.AddHttpClient("SportyBetBooking")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        services.AddSingleton<SportyBetBookingClient>();
        services.AddSingleton<SportyBetAuthClient>();
        services.AddSingleton<IAutomationGateway, SportyBetAutomationGateway>();

        return services;
    }
}
