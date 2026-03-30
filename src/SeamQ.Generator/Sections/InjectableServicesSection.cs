using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class InjectableServicesSection : IIcdSection
{
    public string Title => "Injectable Services";
    public int Order => 60;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var interfaces = seam.ContractSurface.Interfaces.ToList();

        if (interfaces.Count == 0)
        {
            return Task.FromResult("""
                ## 8. Injectable Services

                *No injectable service interfaces detected for this seam.*
                """);
        }

        var methods = seam.ContractSurface.Methods.ToList();
        var sections = new List<string>();

        foreach (var iface in interfaces)
        {
            var ifaceMethods = methods
                .Where(m => m.ParentName == iface.Name)
                .ToList();

            var header = $"""
                ### {iface.Name}

                - **Source:** `{iface.SourceFile}:{iface.LineNumber}`
                - **Documentation:** {iface.Documentation ?? "*No documentation available*"}
                """;

            if (ifaceMethods.Count > 0)
            {
                var methodRows = ifaceMethods.Select(m =>
                    $"| `{EscapePipe(m.Name)}` | `{EscapePipe(m.TypeSignature ?? "void")}` | {EscapePipe(m.Documentation ?? "-")} |");

                header += $"""


                    | Method | Signature | Description |
                    |--------|-----------|-------------|
                    {string.Join("\n", methodRows)}
                    """;
            }
            else
            {
                header += "\n\n*No methods detected for this interface.*";
            }

            sections.Add(header);
        }

        var md = $"""
            ## 8. Injectable Services

            Service interfaces exposed by this seam that consumers may inject and invoke.

            {string.Join("\n\n", sections)}
            """;

        return Task.FromResult(md);
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|");
}
