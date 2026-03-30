using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class ValidateCommand
{
    public static Command Create()
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to validate");
        var allOption = new Option<bool>("--all", "Validate all seams");

        var command = new Command("validate", "Check consumer contract compliance")
        {
            seamIdArgument,
            allOption
        };

        command.SetHandler(async (seamId, all) =>
        {
            throw new NotImplementedException("Validate command not yet implemented");
        }, seamIdArgument, allOption);

        return command;
    }
}
