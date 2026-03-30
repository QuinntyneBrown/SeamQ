using Microsoft.Extensions.DependencyInjection;
using SeamQ.Core.Abstractions;

namespace SeamQ.Differ.DependencyInjection;

public static class DifferServiceCollectionExtensions
{
    public static IServiceCollection AddSeamQDiffer(this IServiceCollection services)
    {
        services.AddSingleton<ISeamDiffer, SeamDiffer>();
        return services;
    }
}
