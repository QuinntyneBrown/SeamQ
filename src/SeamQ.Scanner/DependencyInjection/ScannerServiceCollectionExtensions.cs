using Microsoft.Extensions.DependencyInjection;
using SeamQ.Core.Abstractions;
using SeamQ.Scanner;
using SeamQ.Scanner.Parsing;
using SeamQ.Scanner.TypeScript;

namespace SeamQ.Scanner.DependencyInjection;

public static class ScannerServiceCollectionExtensions
{
    public static IServiceCollection AddSeamQScanner(this IServiceCollection services)
    {
        services.AddSingleton<IWorkspaceScanner, WorkspaceScanner>();
        services.AddSingleton<AngularWorkspaceParser>();
        services.AddSingleton<NxWorkspaceParser>();
        services.AddSingleton<TsConfigResolver>();
        services.AddSingleton<BarrelExportParser>();
        services.AddSingleton<TypeScriptAstParser>();
        services.AddSingleton<AngularMetadataExtractor>();
        return services;
    }
}
