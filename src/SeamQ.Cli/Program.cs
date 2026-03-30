using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeamQ.Cli;
using SeamQ.Cli.Rendering;
using SeamQ.Detector.DependencyInjection;
using SeamQ.Differ.DependencyInjection;
using SeamQ.Generator.DependencyInjection;
using SeamQ.Renderer.DependencyInjection;
using SeamQ.Scanner.DependencyInjection;
using SeamQ.Validator.DependencyInjection;

var services = new ServiceCollection();

// Logging
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

// CLI rendering
services.AddSingleton<IConsoleRenderer, ConsoleRenderer>();

// Global options context
services.AddSingleton<GlobalContext>();

// Core configuration
services.AddSeamQServices();

// Module services
services.AddSeamQScanner();
services.AddSeamQDetector();
services.AddSeamQGenerator();
services.AddSeamQRenderer();
services.AddSeamQDiffer();
services.AddSeamQValidator();

// Data exporter (not registered by a module extension)
services.AddSingleton<SeamQ.Core.Abstractions.IDataExporter, SeamQ.Generator.JsonDataExporter>();

var serviceProvider = services.BuildServiceProvider();

// Auto-load persisted registry if it exists
var registry = serviceProvider.GetRequiredService<SeamQ.Detector.SeamRegistry>();
var registryPath = SeamQ.Detector.SeamRegistry.DefaultRegistryPath;
if (File.Exists(registryPath))
{
    await registry.LoadFromFileAsync(registryPath);
}

CommandBuilder.BuildRootCommand(serviceProvider, out var parser);
return await parser.InvokeAsync(args);
