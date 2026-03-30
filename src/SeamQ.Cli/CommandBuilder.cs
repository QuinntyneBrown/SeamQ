using System.CommandLine;
using SeamQ.Cli.Commands;

namespace SeamQ.Cli;

/// <summary>
/// Builds the root command tree for the SeamQ CLI.
/// </summary>
public static class CommandBuilder
{
    public static RootCommand BuildRootCommand()
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
        rootCommand.AddCommand(ScanCommand.Create());
        rootCommand.AddCommand(ListCommand.Create());
        rootCommand.AddCommand(GenerateCommand.Create());
        rootCommand.AddCommand(DiagramCommand.Create());
        rootCommand.AddCommand(InspectCommand.Create());
        rootCommand.AddCommand(ValidateCommand.Create());
        rootCommand.AddCommand(DiffCommand.Create());
        rootCommand.AddCommand(InitCommand.Create());
        rootCommand.AddCommand(ExportCommand.Create());
        rootCommand.AddCommand(ServeCommand.Create());

        return rootCommand;
    }
}
