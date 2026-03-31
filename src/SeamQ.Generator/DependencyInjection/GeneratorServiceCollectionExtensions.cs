using Microsoft.Extensions.DependencyInjection;
using SeamQ.Core.Abstractions;
using SeamQ.Generator.Formatters;
using SeamQ.Generator.Sections;

namespace SeamQ.Generator.DependencyInjection;

public static class GeneratorServiceCollectionExtensions
{
    /// <summary>
    /// Registers all SeamQ.Generator services: the ICD generator, all built-in sections,
    /// and all built-in output formatters.
    /// </summary>
    public static IServiceCollection AddSeamQGenerator(this IServiceCollection services)
    {
        // Core generators
        services.AddTransient<IIcdGenerator, IcdGenerator>();
        services.AddTransient<IPublicApiGenerator, PublicApiGenerator>();
        services.AddTransient<IDocGenerator, DocGenerator>();
        services.AddTransient<IPublicIcdGenerator, PublicIcdGenerator>();

        // Sections (ordered by their Order property at runtime)
        services.AddTransient<IIcdSection, IntroductionSection>();
        services.AddTransient<IIcdSection, InterfaceOverviewSection>();
        services.AddTransient<IIcdSection, ScopeOfResponsibilitySection>();
        services.AddTransient<IIcdSection, RegistrationContractSection>();
        services.AddTransient<IIcdSection, ComponentInputContractSection>();
        services.AddTransient<IIcdSection, InjectableServicesSection>();
        services.AddTransient<IIcdSection, DataObjectsSection>();
        services.AddTransient<IIcdSection, LifecycleStateManagementSection>();
        services.AddTransient<IIcdSection, ProtocolsSection>();
        services.AddTransient<IIcdSection, TraceabilityMatrixSection>();
        services.AddTransient<IIcdSection, DiagramIndexSection>();

        // Output formatters
        services.AddTransient<IOutputFormatter, MarkdownFormatter>();
        services.AddTransient<IOutputFormatter, HtmlFormatter>();

        return services;
    }
}
