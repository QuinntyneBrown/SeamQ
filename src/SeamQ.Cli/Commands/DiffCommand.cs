using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class DiffCommand
{
    public static Command Create()
    {
        var baselineArgument = new Argument<string>("baseline-path", "Path to baseline JSON file");

        var command = new Command("diff", "Compare scan against a previous baseline")
        {
            baselineArgument
        };

        command.SetHandler(async (baselinePath) =>
        {
            await Task.CompletedTask; throw new NotImplementedException("Diff command not yet implemented");
        }, baselineArgument);

        return command;
    }
}
