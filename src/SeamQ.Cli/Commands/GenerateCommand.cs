using System.CommandLine;

namespace SeamQ.Cli.Commands;

public static class GenerateCommand
{
    public static Command Create()
    {
        var seamIdArgument = new Argument<string?>("seam-id", () => null, "Seam ID to generate ICD for");
        var allOption = new Option<bool>("--all", "Generate ICDs for all detected seams");
        var formatOption = new Option<string[]>("--format", () => new[] { "md" }, "Output format(s): md, html, pdf, docx")
        {
            Arity = ArgumentArity.OneOrMore
        };

        var command = new Command("generate", "Generate ICD for a seam")
        {
            seamIdArgument,
            allOption,
            formatOption
        };

        command.SetHandler(async (seamId, all, formats) =>
        {
            await Task.CompletedTask; throw new NotImplementedException("Generate command not yet implemented");
        }, seamIdArgument, allOption, formatOption);

        return command;
    }
}
