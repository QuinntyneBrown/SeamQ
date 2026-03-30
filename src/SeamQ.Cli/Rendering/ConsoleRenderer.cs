namespace SeamQ.Cli.Rendering;

/// <summary>
/// Terminal output renderer with ANSI color support matching the CLI UI design.
/// Color scheme: green (#10B981) for success/commands, amber (#F59E0B) for warnings/options,
/// cyan (#06B6D4) for info, red for errors, gray for muted text.
/// </summary>
public class ConsoleRenderer : IConsoleRenderer
{
    private readonly TextWriter _out;
    public bool UseColor { get; set; } = true;

    public ConsoleRenderer(TextWriter? output = null)
    {
        _out = output ?? Console.Out;
    }

    public void WriteSuccess(string message) =>
        WriteColored($"[ok] {message}", "\u001b[32m");

    public void WriteError(string message) =>
        WriteColored($"[error] {message}", "\u001b[31m");

    public void WriteWarning(string message) =>
        WriteColored($"[!!] {message}", "\u001b[33m");

    public void WriteInfo(string message) =>
        WriteColored(message, "\u001b[36m");

    public void WriteMuted(string message) =>
        WriteColored(message, "\u001b[90m");

    public void WriteHeader(string title) =>
        WriteColored($"── {title} ──────────────────────────", "\u001b[32m");

    public void WriteKeyValue(string key, string value, int keyWidth = 12)
    {
        if (UseColor)
        {
            _out.Write($"\u001b[90m{key.PadRight(keyWidth)}\u001b[0m");
            _out.WriteLine(value);
        }
        else
        {
            _out.WriteLine($"{key.PadRight(keyWidth)}{value}");
        }
    }

    public void WriteTable(string[] headers, IEnumerable<string[]> rows)
    {
        var widths = new int[headers.Length];
        var allRows = rows.ToList();

        // Calculate column widths
        for (int i = 0; i < headers.Length; i++)
        {
            widths[i] = headers[i].Length;
            foreach (var row in allRows)
            {
                if (i < row.Length && row[i].Length > widths[i])
                    widths[i] = row[i].Length;
            }
            widths[i] += 2; // padding
        }

        // Header
        WriteColored(string.Join("", headers.Select((h, i) => h.PadRight(widths[i]))), "\u001b[90m");

        // Separator
        WriteColored(new string('─', widths.Sum()), "\u001b[90m");

        // Rows
        foreach (var row in allRows)
        {
            var line = string.Join("", row.Select((cell, i) =>
                i < widths.Length ? cell.PadRight(widths[i]) : cell));
            _out.WriteLine(line);
        }
    }

    public void WriteLine(string text = "") => _out.WriteLine(text);

    private void WriteColored(string text, string ansiCode)
    {
        if (UseColor)
            _out.WriteLine($"{ansiCode}{text}\u001b[0m");
        else
            _out.WriteLine(text);
    }
}
