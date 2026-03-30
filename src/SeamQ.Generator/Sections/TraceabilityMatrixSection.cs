using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class TraceabilityMatrixSection : IIcdSection
{
    public string Title => "Traceability Matrix";
    public int Order => 100;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var elements = seam.ContractSurface.Elements;

        if (elements.Count == 0)
        {
            return Task.FromResult("""
                ## 12. Traceability Matrix

                *No contract elements to map.*
                """);
        }

        var rows = elements
            .OrderBy(e => e.SourceFile)
            .ThenBy(e => e.LineNumber)
            .Select(e =>
                $"| `{EscapePipe(e.Name)}` | {e.Kind} | `{EscapePipe(e.Workspace)}` | `{EscapePipe(e.SourceFile)}` | {e.LineNumber} |");

        var md = $"""
            ## 12. Traceability Matrix

            Mapping of every contract element back to its source file for audit and change-tracking purposes.

            | Element | Kind | Workspace | Source File | Line |
            |---------|------|-----------|-------------|-----:|
            {string.Join("\n", rows)}

            **Total elements:** {elements.Count}
            """;

        return Task.FromResult(md);
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|");
}
