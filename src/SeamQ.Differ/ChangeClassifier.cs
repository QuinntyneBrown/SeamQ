using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;

namespace SeamQ.Differ;

/// <summary>
/// Compares two ContractSurface instances element by element
/// and classifies changes as Added, Modified, or Removed.
/// </summary>
public static class ChangeClassifier
{
    /// <summary>
    /// Compares a baseline contract surface against a current one
    /// and returns the list of classified changes.
    /// </summary>
    public static IReadOnlyList<SeamChange> Classify(ContractSurface baseline, ContractSurface current)
    {
        var changes = new List<SeamChange>();

        var baselineByKey = baseline.Elements.ToDictionary(e => GetElementKey(e));
        var currentByKey = current.Elements.ToDictionary(e => GetElementKey(e));

        // Find added elements (in current but not in baseline)
        foreach (var (key, element) in currentByKey)
        {
            if (!baselineByKey.ContainsKey(key))
            {
                changes.Add(new SeamChange
                {
                    ElementName = element.Name,
                    ChangeType = ChangeType.Added,
                    NewValue = FormatElementValue(element)
                });
            }
        }

        // Find removed elements (in baseline but not in current)
        foreach (var (key, element) in baselineByKey)
        {
            if (!currentByKey.ContainsKey(key))
            {
                changes.Add(new SeamChange
                {
                    ElementName = element.Name,
                    ChangeType = ChangeType.Removed,
                    OldValue = FormatElementValue(element)
                });
            }
        }

        // Find modified elements (in both, but different)
        foreach (var (key, baselineElement) in baselineByKey)
        {
            if (currentByKey.TryGetValue(key, out var currentElement))
            {
                if (!AreElementsEquivalent(baselineElement, currentElement))
                {
                    changes.Add(new SeamChange
                    {
                        ElementName = currentElement.Name,
                        ChangeType = ChangeType.Modified,
                        OldValue = FormatElementValue(baselineElement),
                        NewValue = FormatElementValue(currentElement)
                    });
                }
            }
        }

        return changes.OrderBy(c => c.ElementName).ThenBy(c => c.ChangeType).ToList();
    }

    /// <summary>
    /// Creates a unique key for an element based on its name, kind, and parent.
    /// </summary>
    private static string GetElementKey(ContractElement element)
    {
        return $"{element.Kind}:{element.ParentName ?? ""}:{element.Name}:{element.SourceFile ?? ""}";
    }

    /// <summary>
    /// Determines if two elements are functionally equivalent.
    /// </summary>
    private static bool AreElementsEquivalent(ContractElement baseline, ContractElement current)
    {
        return baseline.TypeSignature == current.TypeSignature
            && baseline.Kind == current.Kind
            && baseline.Documentation == current.Documentation;
    }

    /// <summary>
    /// Formats an element value for display in diff output.
    /// </summary>
    private static string FormatElementValue(ContractElement element)
    {
        var parts = new List<string> { $"{element.Kind}: {element.Name}" };

        if (!string.IsNullOrEmpty(element.TypeSignature))
        {
            parts.Add($"type={element.TypeSignature}");
        }

        if (!string.IsNullOrEmpty(element.ParentName))
        {
            parts.Add($"parent={element.ParentName}");
        }

        return string.Join(", ", parts);
    }
}
