using Microsoft.Extensions.DependencyInjection;
using SeamQ.Core.Abstractions;
using SeamQ.Detector.Strategies;

namespace SeamQ.Detector.DependencyInjection;

public static class DetectorServiceCollectionExtensions
{
    public static IServiceCollection AddSeamQDetector(this IServiceCollection services)
    {
        services.AddSingleton<ISeamDetector, SeamDetector>();
        services.AddSingleton<SeamRegistry>();
        services.AddSingleton<ConfidenceScorer>();
        services.AddSingleton<ISeamDetectionStrategy, PluginContractStrategy>();
        services.AddSingleton<ISeamDetectionStrategy, SharedLibraryStrategy>();
        services.AddSingleton<ISeamDetectionStrategy, MessageBusStrategy>();
        services.AddSingleton<ISeamDetectionStrategy, RouteContractStrategy>();
        services.AddSingleton<ISeamDetectionStrategy, StateContractStrategy>();
        services.AddSingleton<ISeamDetectionStrategy, HttpApiContractStrategy>();
        return services;
    }
}
