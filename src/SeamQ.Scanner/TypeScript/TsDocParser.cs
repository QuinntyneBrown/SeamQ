using System.Text.RegularExpressions;

namespace SeamQ.Scanner.TypeScript;

/// <summary>
/// Extracts JSDoc/TSDoc block comments (/** ... */) preceding declarations.
/// </summary>
public static partial class TsDocParser
{
    // Matches a /** ... */ block comment (potentially multiline)
    [GeneratedRegex(@"/\*\*(.*?)\*/", RegexOptions.Singleline)]
    private static partial Regex DocBlockRegex();

    // Strips leading * and whitespace from each line of a doc comment
    [GeneratedRegex(@"^\s*\*\s?", RegexOptions.Multiline)]
    private static partial Regex LeadingAsteriskRegex();

    /// <summary>
    /// Extracts the TSDoc comment block that immediately precedes the given line index
    /// in the source text lines array.
    /// </summary>
    /// <param name="lines">All lines of the source file.</param>
    /// <param name="declarationLineIndex">Zero-based index of the declaration line.</param>
    /// <returns>The cleaned doc comment text, or null if none found.</returns>
    public static string? ExtractDocComment(string[] lines, int declarationLineIndex)
    {
        if (declarationLineIndex <= 0 || declarationLineIndex >= lines.Length)
        {
            return null;
        }

        // Walk backwards from the line before the declaration to find a /** ... */ block
        var endIndex = -1;
        var startIndex = -1;

        for (var i = declarationLineIndex - 1; i >= 0; i--)
        {
            var trimmed = lines[i].Trim();

            // Skip empty lines and decorators between comment and declaration
            if (endIndex < 0)
            {
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('@'))
                {
                    continue;
                }

                if (trimmed.EndsWith("*/"))
                {
                    endIndex = i;
                }
                else
                {
                    // Non-comment, non-empty content — no doc comment
                    return null;
                }
            }

            if (endIndex >= 0)
            {
                if (trimmed.StartsWith("/**") || trimmed.StartsWith("/*"))
                {
                    startIndex = i;
                    break;
                }
            }
        }

        if (startIndex < 0 || endIndex < 0)
        {
            return null;
        }

        // Reconstruct the comment block
        var commentBlock = string.Join('\n', lines[startIndex..(endIndex + 1)]);

        return CleanDocComment(commentBlock);
    }

    /// <summary>
    /// Cleans a raw doc comment block, removing delimiters and leading asterisks.
    /// </summary>
    public static string CleanDocComment(string rawComment)
    {
        // Remove the /** and */ delimiters
        var match = DocBlockRegex().Match(rawComment);
        if (!match.Success)
        {
            return rawComment.Trim();
        }

        var inner = match.Groups[1].Value;

        // Remove leading asterisks
        var cleaned = LeadingAsteriskRegex().Replace(inner, "");

        // Trim and normalize whitespace
        var lines = cleaned.Split('\n')
            .Select(l => l.TrimEnd())
            .ToArray();

        // Remove leading/trailing empty lines
        var start = 0;
        while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start]))
            start++;

        var end = lines.Length - 1;
        while (end >= start && string.IsNullOrWhiteSpace(lines[end]))
            end--;

        if (start > end)
        {
            return string.Empty;
        }

        return string.Join('\n', lines[start..(end + 1)]);
    }
}
