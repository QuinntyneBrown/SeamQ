using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class ExportCommand
{
    public static Command Create()
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to export");
        var allOption = new Option<bool>("--all", "Export all seams");
        var formatOption = new Option<string>("--format", () => "json", "Export format");

        var command = new Command("export", "Export raw seam data as JSON")
        {
            seamIdArgument,
            allOption,
            formatOption
        };

        command.SetHandler(async (seamId, all, format) =>
        {
            throw new NotImplementedException("Export command not yet implemented");
        }, seamIdArgument, allOption, formatOption);

        return command;
    }
}
