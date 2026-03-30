using Microsoft.Extensions.DependencyInjection;
using SeamQ.Core.Abstractions;

namespace SeamQ.Renderer.DependencyInjection;

public static class RendererServiceCollectionExtensions
{
    public static IServiceCollection AddSeamQRenderer(this IServiceCollection services)
    {
        services.AddSingleton<IDiagramRenderer, DiagramRenderer>();
        return services;
    }
}
