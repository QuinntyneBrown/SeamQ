namespace SeamQ.Cli;

/// <summary>
/// Stores global CLI option values parsed from the command line,
/// made available to all commands via dependency injection.
/// </summary>
public class GlobalContext
{
    public bool Verbose { get; set; }
    public bool Quiet { get; set; }
    public bool NoColor { get; set; }
    public string? OutputDir { get; set; }
    public string? ConfigPath { get; set; }
}
