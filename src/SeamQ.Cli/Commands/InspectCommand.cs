using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class InspectCommand
{
    public static Command Create()
    {
        var seamIdArgument = new Argument<string>("seam-id", "Seam ID to inspect");

        var command = new Command("inspect", "Print detailed contract surface for a seam")
        {
            seamIdArgument
        };

        command.SetHandler(async (seamId) =>
        {
            await Task.CompletedTask; throw new NotImplementedException("Inspect command not yet implemented");
        }, seamIdArgument);

        return command;
    }
}
