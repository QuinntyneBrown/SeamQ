namespace SeamQ.Generator.Formatters;

public interface IOutputFormatter
{
    string Format { get; }
    Task<string> FormatAsync(string markdownContent, string title, CancellationToken cancellationToken = default);
    string GetFileExtension();
}
