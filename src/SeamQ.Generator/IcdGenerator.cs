using System.Text;
using SeamQ.Core.Abstractions;
using SeamQ.Core.Models;
using SeamQ.Generator.Formatters;
using SeamQ.Generator.Sections;

namespace SeamQ.Generator;

public class IcdGenerator : IIcdGenerator
{
    private readonly IEnumerable<IIcdSection> _sections;
    private readonly IEnumerable<IOutputFormatter> _formatters;

    public IcdGenerator(IEnumerable<IIcdSection> sections, IEnumerable<IOutputFormatter> formatters)
    {
        _sections = sections;
        _formatters = formatters;
    }

    public async Task GenerateAsync(
        Seam seam,
        string outputDirectory,
        IReadOnlyList<string> formats,
        CancellationToken cancellationToken = default)
    {
        // Generate markdown content by iterating all registered sections in order
        var orderedSections = _sections.OrderBy(s => s.Order).ToList();
        var contentBuilder = new StringBuilder();

        foreach (var section in orderedSections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sectionContent = await section.GenerateAsync(seam, cancellationToken);
            if (!string.IsNullOrWhiteSpace(sectionContent))
            {
                if (contentBuilder.Length > 0)
                {
                    contentBuilder.AppendLine();
                    contentBuilder.AppendLine();
                }
                contentBuilder.Append(sectionContent);
            }
        }

        var markdownContent = contentBuilder.ToString();
        var title = $"{seam.Name} - Interface Control Document";

        // Ensure output directory exists
        Directory.CreateDirectory(outputDirectory);

        // Build a lookup of available formatters
        var formatterLookup = _formatters.ToDictionary(f => f.Format, f => f, StringComparer.OrdinalIgnoreCase);

        // Write output for each requested format
        foreach (var format in formats)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!formatterLookup.TryGetValue(format, out var formatter))
            {
                throw new InvalidOperationException(
                    $"No output formatter registered for format '{format}'. " +
                    $"Available formats: {string.Join(", ", formatterLookup.Keys)}");
            }

            var formattedContent = await formatter.FormatAsync(markdownContent, title, cancellationToken);
            var fileName = SanitizeFileName(seam.Name) + formatter.GetFileExtension();
            var filePath = Path.Combine(outputDirectory, fileName);

            await File.WriteAllTextAsync(filePath, formattedContent, Encoding.UTF8, cancellationToken);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(name.Length);

        foreach (var c in name)
        {
            if (Array.IndexOf(invalidChars, c) >= 0 || c == ' ')
            {
                sanitized.Append('-');
            }
            else
            {
                sanitized.Append(char.ToLowerInvariant(c));
            }
        }

        return sanitized.ToString();
    }
}
