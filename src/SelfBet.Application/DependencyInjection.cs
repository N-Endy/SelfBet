using Microsoft.Extensions.DependencyInjection;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Services;
using SelfBet.Application.UseCases;

namespace SelfBet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSelfBetApplication(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureBuilder, FeatureBuilder>();
        services.AddSingleton<ISlipOptimizer, SlipOptimizer>();
        services.AddSingleton<ISafetyGate, SafetyGate>();

        // Prediction uses calibration, both scoped so calibration profiles are consistent per request
        services.AddScoped<IPredictionService, PoissonPredictionService>();

        services.AddScoped<IBankrollService, BankrollService>();
        services.AddScoped<RunEngine>();
        services.AddScoped<PlaceSlipUseCase>();
        services.AddScoped<CancelSlipUseCase>();
        services.AddScoped<PerformanceQuery>();
        services.AddScoped<UpdateStrategyConfigUseCase>();
        services.AddScoped<ToggleAutomationUseCase>();

        return services;
    }
}
