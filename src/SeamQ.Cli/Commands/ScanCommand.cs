using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class ScanCommand
{
    public static Command Create()
    {
        var pathsArgument = new Argument<string[]>("paths", "One or more workspace root paths")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        var saveBaselineOption = new Option<string?>("--save-baseline", "Save scan result as baseline JSON");
        var noCacheOption = new Option<bool>("--no-cache", "Disable AST caching");
        var excludeOption = new Option<string[]>("--exclude", "Glob patterns to exclude paths")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("scan", "Scan workspaces and build the seam registry")
        {
            pathsArgument,
            saveBaselineOption,
            noCacheOption,
            excludeOption
        };

        command.SetHandler(async (paths, saveBaseline, noCache, exclude) =>
        {
            // TODO: Implement scan command
            throw new NotImplementedException("Scan command not yet implemented");
        }, pathsArgument, saveBaselineOption, noCacheOption, excludeOption);

        return command;
    }
}
