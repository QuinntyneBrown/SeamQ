using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class ComponentInputContractSection : IIcdSection
{
    public string Title => "Component Input/Output Contract";
    public int Order => 50;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var inputs = seam.ContractSurface.InputBindings.ToList();
        var outputs = seam.ContractSurface.OutputBindings.ToList();

        if (inputs.Count == 0 && outputs.Count == 0)
        {
            return Task.FromResult("""
                ## 7. Component Input/Output Contract

                *No component input or output bindings detected for this seam.*
                """);
        }

        var inputRows = inputs.Select(e =>
            $"| `{EscapePipe(e.Name)}` | Input | `{EscapePipe(e.TypeSignature ?? "any")}` | `{EscapePipe(e.ParentName ?? "-")}` | {EscapePipe(e.Documentation ?? "-")} |");

        var outputRows = outputs.Select(e =>
            $"| `{EscapePipe(e.Name)}` | Output | `{EscapePipe(e.TypeSignature ?? "EventEmitter")}` | `{EscapePipe(e.ParentName ?? "-")}` | {EscapePipe(e.Documentation ?? "-")} |");

        var allRows = string.Join("\n", inputRows.Concat(outputRows));

        var md = $"""
            ## 7. Component Input/Output Contract

            Bindings that define the component-level public API for this seam.

            | Name | Direction | Type | Component | Description |
            |------|-----------|------|-----------|-------------|
            {allRows}
            """;

        return Task.FromResult(md);
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|");
}
