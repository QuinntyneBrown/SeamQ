using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public class IntroductionSection : IIcdSection
{
    public string Title => "Introduction";
    public int Order => 10;

    public Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default)
    {
        var consumers = seam.Consumers.Count > 0
            ? string.Join(", ", seam.Consumers.Select(c => $"`{c.Alias}`"))
            : "*None registered*";

        var elementCount = seam.ContractSurface.Elements.Count;

        var md = $"""
            # {seam.Name} - Interface Control Document

            ## 1. Purpose

            This Interface Control Document (ICD) defines the contract boundary for the **{seam.Name}** seam
            (ID: `{seam.Id}`). It specifies all integration points, data structures, and protocols that govern
            how the provider and consumer workspaces interact across this seam.

            ## 2. Scope

            | Attribute | Value |
            |-----------|-------|
            | Seam ID | `{seam.Id}` |
            | Seam Type | {seam.Type} |
            | Provider | `{seam.Provider.Alias}` ({seam.Provider.Role}) |
            | Consumer(s) | {consumers} |
            | Confidence | {seam.Confidence:P0} |
            | Total Contract Elements | {elementCount} |

            ## 3. System Overview

            The **{seam.Name}** seam is a **{seam.Type}** boundary provided by the `{seam.Provider.Alias}`
            workspace (type: {seam.Provider.Type}, role: {seam.Provider.Role}). It exposes {elementCount}
            contract element(s) that define the integration surface consumed by downstream workspaces.
            """;

        return Task.FromResult(md);
    }
}
