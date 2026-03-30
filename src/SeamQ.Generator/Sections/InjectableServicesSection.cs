using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class InjectableServicesSection : IIcdSection
{
    public string Title => "Injectable Services";
    public int Order => 60;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        // Injectable services are elements that have child methods/properties (ParentName matches)
        // or interfaces/abstract classes that have method children.
        var methods = seam.ContractSurface.Methods.ToList();
        var properties = seam.ContractSurface.Elements
            .Where(e => e.Kind == ContractElementKind.Property)
            .ToList();

        // Find parent types that have methods or properties — these are service-like
        var serviceParentNames = methods.Select(m => m.ParentName)
            .Concat(properties.Select(p => p.ParentName))
            .Where(n => n is not null)
            .ToHashSet();

        var services = seam.ContractSurface.Elements
            .Where(e => serviceParentNames.Contains(e.Name) ||
                        // Also include interfaces/abstract classes with methods
                        (e.Kind is ContractElementKind.Interface or ContractElementKind.AbstractClass
                         && methods.Any(m => m.ParentName == e.Name)))
            .DistinctBy(e => e.Name)
            .ToList();

        // If no services found with methods, fall back: show interfaces that look like service contracts
        // (have methods as siblings from the same parent)
        if (services.Count == 0)
        {
            return Task.FromResult("""
                ## 8. Injectable Services

                *No injectable service interfaces detected for this seam.*
                """);
        }

        var sections = new List<string>();

        foreach (var svc in services)
        {
            var svcMethods = methods
                .Where(m => m.ParentName == svc.Name)
                .ToList();
            var svcProperties = properties
                .Where(p => p.ParentName == svc.Name)
                .ToList();

            var header = $"""
                ### {svc.Name}

                - **Source:** `{svc.SourceFile}:{svc.LineNumber}`
                - **Kind:** {svc.Kind}
                - **Documentation:** {svc.Documentation ?? "*No documentation available*"}
                """;

            if (svcMethods.Count > 0)
            {
                var methodRows = svcMethods.Select(m =>
                    $"| `{EscapePipe(m.Name)}` | `{EscapePipe(m.TypeSignature ?? "void")}` | {EscapePipe(m.Documentation ?? "-")} |");

                header += $"""


                    | Method | Signature | Description |
                    |--------|-----------|-------------|
                    {string.Join("\n", methodRows)}
                    """;
            }

            if (svcProperties.Count > 0)
            {
                var propRows = svcProperties.Select(p =>
                    $"| `{EscapePipe(p.Name)}` | `{EscapePipe(p.TypeSignature ?? "-")}` | {EscapePipe(p.Documentation ?? "-")} |");

                header += $"""


                    | Property | Type | Description |
                    |----------|------|-------------|
                    {string.Join("\n", propRows)}
                    """;
            }

            if (svcMethods.Count == 0 && svcProperties.Count == 0)
            {
                header += "\n\n*No methods or properties detected for this service.*";
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
