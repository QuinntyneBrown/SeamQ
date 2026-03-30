using SeamQ.Core.Models;

namespace SeamQ.Renderer;

/// <summary>
/// Provides name-based heuristic classification for contract elements
/// when the scanner classifies everything as Type.
/// </summary>
internal static class TypeClassifier
{
    public static bool IsService(ContractElement e) =>
        e.ParentName is null && e.Name.EndsWith("Service", StringComparison.Ordinal);

    public static bool IsComponent(ContractElement e) =>
        e.ParentName is null && e.Name.EndsWith("Component", StringComparison.Ordinal);

    public static bool IsRequestMessage(ContractElement e) =>
        e.ParentName is null &&
        (e.Name.EndsWith("Message", StringComparison.Ordinal) ||
         (e.Name.EndsWith("Request", StringComparison.Ordinal) && !e.Name.Contains("Response")));

    public static bool IsResponse(ContractElement e) =>
        e.ParentName is null && e.Name.Contains("Response", StringComparison.Ordinal);

    public static bool IsEnumLike(ContractElement e) =>
        e.ParentName is null &&
        (e.Name.EndsWith("Status", StringComparison.Ordinal) ||
         e.Name.EndsWith("State", StringComparison.Ordinal) ||
         e.Name.EndsWith("Code", StringComparison.Ordinal) ||
         e.Name.EndsWith("Type", StringComparison.Ordinal));

    public static bool IsDataObject(ContractElement e) =>
        e.ParentName is null && !IsService(e) && !IsComponent(e);

    public static bool IsMember(ContractElement e) =>
        e.ParentName is not null;

    /// <summary>
    /// Groups top-level elements with their child members by ParentName.
    /// Returns (parent, children[]) tuples.
    /// </summary>
    public static IReadOnlyList<(ContractElement Parent, IReadOnlyList<ContractElement> Members)> GroupWithMembers(
        IEnumerable<ContractElement> elements)
    {
        var all = elements.ToList();
        var topLevel = all.Where(e => e.ParentName is null).ToList();
        var children = all.Where(e => e.ParentName is not null)
            .GroupBy(e => e.ParentName!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ContractElement>)g.ToList());

        return topLevel.Select(p => (
            Parent: p,
            Members: children.GetValueOrDefault(p.Name, Array.Empty<ContractElement>())
        )).ToList();
    }

    /// <summary>
    /// Gets all service-like elements (either classified Methods with ParentName, or Types named *Service).
    /// </summary>
    public static IReadOnlyList<ContractElement> GetServices(ContractSurface surface) =>
        surface.Interfaces.Where(i => i.Name.EndsWith("Service", StringComparison.Ordinal))
            .Concat(surface.Elements.Where(e => e.Kind == ContractElementKind.Type && IsService(e)))
            .DistinctBy(e => e.Name)
            .ToList();

    /// <summary>
    /// Finds message/response type pairs (e.g., CommandMessage ↔ CommandResponse).
    /// </summary>
    public static IReadOnlyList<(ContractElement Request, ContractElement? Response)> GetMessagePairs(
        ContractSurface surface)
    {
        var messages = surface.Elements.Where(e => IsRequestMessage(e)).ToList();
        var responses = surface.Elements.Where(e => IsResponse(e)).ToList();

        return messages.Select(msg =>
        {
            var prefix = ExtractPrefix(msg.Name);
            var response = !string.IsNullOrEmpty(prefix)
                ? responses.FirstOrDefault(r => r.Name.StartsWith(prefix, StringComparison.Ordinal))
                : null;
            return (Request: msg, Response: response);
        }).ToList();
    }

    /// <summary>
    /// Finds the service that likely handles a given message type by name prefix matching.
    /// E.g., CommandMessage → CommandService, RequestMessage → RequestService
    /// </summary>
    public static ContractElement? FindServiceForMessage(ContractElement message, IEnumerable<ContractElement> services)
    {
        var prefix = ExtractPrefix(message.Name);
        if (string.IsNullOrEmpty(prefix)) return null;
        return services.FirstOrDefault(s =>
            s.Name.StartsWith(prefix, StringComparison.Ordinal) &&
            s.Name.EndsWith("Service", StringComparison.Ordinal));
    }

    /// <summary>
    /// Extracts the domain prefix from a message/request type name.
    /// CommandMessage → Command, QueryMessage → Query, RequestMessage → Request,
    /// HistoricalTelemetryRequest → HistoricalTelemetry
    /// </summary>
    private static string ExtractPrefix(string name)
    {
        if (name.EndsWith("Message", StringComparison.Ordinal))
            return name[..^"Message".Length];
        if (name.EndsWith("Request", StringComparison.Ordinal))
            return name[..^"Request".Length];
        return name;
    }
}
