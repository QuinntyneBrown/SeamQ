namespace SeamQ.Cli.Rendering;

/// <summary>
/// Abstraction for formatted console output with color support.
/// </summary>
public interface IConsoleRenderer
{
    void WriteSuccess(string message);
    void WriteError(string message);
    void WriteWarning(string message);
    void WriteInfo(string message);
    void WriteMuted(string message);
    void WriteHeader(string title);
    void WriteKeyValue(string key, string value, int keyWidth = 12);
    void WriteTable(string[] headers, IEnumerable<string[]> rows);
    void WriteLine(string text = "");
    bool UseColor { get; set; }
}
