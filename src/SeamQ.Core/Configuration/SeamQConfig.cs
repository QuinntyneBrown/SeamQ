namespace SeamQ.Core.Configuration;

public record SeamQConfig
{
    public IReadOnlyList<WorkspaceConfig> Workspaces { get; init; } = [];
    public SeamFilterConfig Seams { get; init; } = new();
    public OutputConfig Output { get; init; } = new();
    public AnalysisConfig Analysis { get; init; } = new();
    public IcdMetadataConfig Icd { get; init; } = new();
}

public record WorkspaceConfig
{
    public required string Path { get; init; }
    public string? Alias { get; init; }
    public string Role { get; init; } = "application";
}

public record SeamFilterConfig
{
    public IReadOnlyList<string> Include { get; init; } = ["plugin-contract", "shared-library", "message-bus", "route-contract", "state-contract", "http-api-contract"];
    public IReadOnlyList<string> Exclude { get; init; } = [];
    public IReadOnlyList<string> CustomDecorators { get; init; } = [];
}

public record OutputConfig
{
    public string Directory { get; init; } = "./seamq-output";
    public IReadOnlyList<string> Formats { get; init; } = ["md"];
    public DiagramOutputConfig Diagrams { get; init; } = new();
}

public record DiagramOutputConfig
{
    public string RenderFormat { get; init; } = "svg";
    public string PlantUmlServer { get; init; } = "local";
    public string? Theme { get; init; }
    public Dictionary<string, object>? Skinparams { get; init; }
}

public record AnalysisConfig
{
    public int MaxDepth { get; init; } = 5;
    public bool FollowNodeModules { get; init; }
    public bool IncludeInternalSeams { get; init; }
    public double ConfidenceThreshold { get; init; } = 0.6;
}

public record IcdMetadataConfig
{
    public string? Title { get; init; }
    public string? DocumentNumber { get; init; }
    public string? Revision { get; init; }
    public string? Classification { get; init; }
    public string? Template { get; init; }
}
