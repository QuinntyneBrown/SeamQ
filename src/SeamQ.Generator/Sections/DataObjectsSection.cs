using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class DataObjectsSection : IIcdSection
{
    public string Title => "Data Objects";
    public int Order => 70;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var types = seam.ContractSurface.Types.ToList();

        if (types.Count == 0)
        {
            return Task.FromResult("""
                ## 9. Data Objects and Type Definitions

                *No shared data types or enums detected for this seam.*
                """);
        }

        var typeElements = types.Where(t => t.Kind == ContractElementKind.Type).ToList();
        var enumElements = types.Where(t => t.Kind == ContractElementKind.Enum).ToList();

        var sections = new List<string>();

        if (typeElements.Count > 0)
        {
            var rows = typeElements.Select(t =>
                $"| `{EscapePipe(t.Name)}` | Type | `{EscapePipe(t.TypeSignature ?? "-")}` | `{EscapePipe(t.SourceFile)}:{t.LineNumber}` | {EscapePipe(t.Documentation ?? "-")} |");

            sections.Add($"""
                ### Types

                | Name | Kind | Signature | Source | Description |
                |------|------|-----------|--------|-------------|
                {string.Join("\n", rows)}
                """);
        }

        if (enumElements.Count > 0)
        {
            var rows = enumElements.Select(e =>
                $"| `{EscapePipe(e.Name)}` | Enum | `{EscapePipe(e.TypeSignature ?? "-")}` | `{EscapePipe(e.SourceFile)}:{e.LineNumber}` | {EscapePipe(e.Documentation ?? "-")} |");

            sections.Add($"""
                ### Enumerations

                | Name | Kind | Signature | Source | Description |
                |------|------|-----------|--------|-------------|
                {string.Join("\n", rows)}
                """);
        }

        var md = $"""
            ## 9. Data Objects and Type Definitions

            Shared data structures and enumerations that form part of this seam's contract.

            {string.Join("\n\n", sections)}
            """;

        return Task.FromResult(md);
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|");
}
