using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using SeamQ.Cli.Commands;
using SeamQ.Cli.Rendering;

namespace SeamQ.Cli;

/// <summary>
/// Builds the root command tree for the SeamQ CLI.
/// </summary>
public static class CommandBuilder
{
    public static Command BuildRootCommand(IServiceProvider serviceProvider, out Parser parser)
    {
        var rootCommand = new RootCommand("seamq — static analysis tool for angular workspace interface boundaries");

        // Global options
        var verboseOption = new Option<bool>("--verbose", "Enable detailed logging output");
        var quietOption = new Option<bool>("--quiet", "Suppress all output except errors");
        var noColorOption = new Option<bool>("--no-color", "Disable ANSI color codes");
        var outputDirOption = new Option<string?>("--output-dir", "Override output directory");
        var configOption = new Option<string?>("--config", "Specify custom config file");
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(quietOption);
        rootCommand.AddGlobalOption(noColorOption);
        rootCommand.AddGlobalOption(outputDirOption);
        rootCommand.AddGlobalOption(configOption);

        // Commands
        rootCommand.AddCommand(ScanCommand.Create(serviceProvider));
        rootCommand.AddCommand(ListCommand.Create(serviceProvider));
        rootCommand.AddCommand(GenerateCommand.Create(serviceProvider));
        rootCommand.AddCommand(DiagramCommand.Create(serviceProvider));
        rootCommand.AddCommand(InspectCommand.Create(serviceProvider));
        rootCommand.AddCommand(ValidateCommand.Create(serviceProvider));
        rootCommand.AddCommand(DiffCommand.Create(serviceProvider));
        rootCommand.AddCommand(InitCommand.Create(serviceProvider));
        rootCommand.AddCommand(ExportCommand.Create(serviceProvider));
        rootCommand.AddCommand(PublicApiCommand.Create(serviceProvider));
        rootCommand.AddCommand(ServeCommand.Create(serviceProvider));

        // Build parser with middleware pipeline
        parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .AddMiddleware(async (context, next) =>
            {
                // Populate GlobalContext from parsed global options
                var gc = serviceProvider.GetRequiredService<GlobalContext>();
                gc.Verbose = context.ParseResult.GetValueForOption(verboseOption);
                gc.Quiet = context.ParseResult.GetValueForOption(quietOption);
                gc.NoColor = context.ParseResult.GetValueForOption(noColorOption);
                gc.OutputDir = context.ParseResult.GetValueForOption(outputDirOption);
                gc.ConfigPath = context.ParseResult.GetValueForOption(configOption);

                // Apply no-color to renderer
                var consoleRenderer = serviceProvider.GetRequiredService<IConsoleRenderer>();
                consoleRenderer.UseColor = !gc.NoColor;

                await next(context);
            })
            .Build();

        return rootCommand;
    }

    /// <summary>
    /// A no-op invocation result that prevents the default handler from running.
    /// Used when --version is handled in middleware.
    /// </summary>
    private sealed class NoOpInvocationResult : IInvocationResult
    {
        public void Apply(InvocationContext context)
        {
            context.ExitCode = 0;
        }
    }
}
