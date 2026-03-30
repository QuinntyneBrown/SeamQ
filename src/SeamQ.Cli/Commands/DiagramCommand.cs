using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class DiagramCommand
{
    public static Command Create()
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to generate diagrams for");
        var allOption = new Option<bool>("--all", "Generate diagrams for all seams");
        var typeOption = new Option<string?>("--type", "Diagram type: context, class, sequence, state, c4-context, c4-container, c4-component, c4-code");

        var command = new Command("diagram", "Generate diagrams for a seam")
        {
            seamIdArgument,
            allOption,
            typeOption
        };

        command.SetHandler(async (seamId, all, type) =>
        {
            throw new NotImplementedException("Diagram command not yet implemented");
        }, seamIdArgument, allOption, typeOption);

        return command;
    }
}
