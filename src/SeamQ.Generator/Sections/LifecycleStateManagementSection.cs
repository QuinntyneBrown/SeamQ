using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class LifecycleStateManagementSection : IIcdSection
{
    public string Title => "Lifecycle and State Management";
    public int Order => 80;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var actions = seam.ContractSurface.Elements
            .Where(e => e.Kind == ContractElementKind.Action)
            .ToList();

        var selectors = seam.ContractSurface.Elements
            .Where(e => e.Kind == ContractElementKind.Selector)
            .ToList();

        var observables = seam.ContractSurface.Elements
            .Where(e => e.Kind == ContractElementKind.Observable)
            .ToList();

        var signalInputs = seam.ContractSurface.Elements
            .Where(e => e.Kind == ContractElementKind.SignalInput)
            .ToList();

        if (actions.Count == 0 && selectors.Count == 0 && observables.Count == 0 && signalInputs.Count == 0)
        {
            return Task.FromResult("""
                ## 10. Lifecycle and State Management

                *No state management elements (actions, selectors, observables, signals) detected for this seam.*
                """);
        }

        var sections = new List<string>();

        if (actions.Count > 0)
        {
            var rows = actions.Select(a =>
                $"| `{EscapePipe(a.Name)}` | `{EscapePipe(a.TypeSignature ?? "-")}` | `{EscapePipe(a.SourceFile)}:{a.LineNumber}` | {EscapePipe(a.Documentation ?? "-")} |");

            sections.Add($"""
                ### State Actions

                | Action | Payload Type | Source | Description |
                |--------|-------------|--------|-------------|
                {string.Join("\n", rows)}
                """);
        }

        if (selectors.Count > 0)
        {
            var rows = selectors.Select(s =>
                $"| `{EscapePipe(s.Name)}` | `{EscapePipe(s.TypeSignature ?? "-")}` | `{EscapePipe(s.SourceFile)}:{s.LineNumber}` | {EscapePipe(s.Documentation ?? "-")} |");

            sections.Add($"""
                ### State Selectors

                | Selector | Return Type | Source | Description |
                |----------|------------|--------|-------------|
                {string.Join("\n", rows)}
                """);
        }

        if (observables.Count > 0)
        {
            var rows = observables.Select(o =>
                $"| `{EscapePipe(o.Name)}` | `{EscapePipe(o.TypeSignature ?? "-")}` | `{EscapePipe(o.ParentName ?? "-")}` | `{EscapePipe(o.SourceFile)}:{o.LineNumber}` |");

            sections.Add($"""
                ### Observable Streams

                | Observable | Type | Parent | Source |
                |-----------|------|--------|--------|
                {string.Join("\n", rows)}
                """);
        }

        if (signalInputs.Count > 0)
        {
            var rows = signalInputs.Select(s =>
                $"| `{EscapePipe(s.Name)}` | `{EscapePipe(s.TypeSignature ?? "-")}` | `{EscapePipe(s.ParentName ?? "-")}` | `{EscapePipe(s.SourceFile)}:{s.LineNumber}` |");

            sections.Add($"""
                ### Signal Inputs

                | Signal | Type | Parent | Source |
                |--------|------|--------|--------|
                {string.Join("\n", rows)}
                """);
        }

        var md = $"""
            ## 10. Lifecycle and State Management

            State management elements, reactive streams, and signal-based inputs that form part of this seam's
            lifecycle contract.

            {string.Join("\n\n", sections)}
            """;

        return Task.FromResult(md);
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|");
}
