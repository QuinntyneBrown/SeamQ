using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class ListCommand
{
    public static Command Create()
    {
        var typeOption = new Option<string?>("--type", "Filter by seam type");
        var providerOption = new Option<string?>("--provider", "Filter by provider workspace");
        var confidenceOption = new Option<double?>("--confidence", "Minimum confidence threshold");

        var command = new Command("list", "List all detected seams")
        {
            typeOption,
            providerOption,
            confidenceOption
        };

        command.SetHandler(async (type, provider, confidence) =>
        {
            throw new NotImplementedException("List command not yet implemented");
        }, typeOption, providerOption, confidenceOption);

        return command;
    }
}
