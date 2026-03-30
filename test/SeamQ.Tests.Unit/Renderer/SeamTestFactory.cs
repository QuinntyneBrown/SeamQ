using SeamQ.Core.Models;

namespace SeamQ.Tests.Unit.Renderer;

internal static class SeamTestFactory
{
    public static Seam CreatePluginSeam() => new()
    {
        Id = "test-plugin-001",
        Name = "TestApp/plugin-contract",
        Type = SeamType.PluginContract,
        Provider = CreateProvider(),
        Consumers = new[] { CreateConsumer("PluginA"), CreateConsumer("PluginB") },
        Confidence = 0.95,
        ContractSurface = CreateRichContractSurface()
    };

    public static Seam CreateHttpApiSeam() => new()
    {
        Id = "test-http-001",
        Name = "TestApp/http-api-contract",
        Type = SeamType.HttpApiContract,
        Provider = CreateProvider(),
        Consumers = new[] { CreateConsumer("WebClient") },
        Confidence = 0.9,
        ContractSurface = CreateHttpContractSurface()
    };

    public static Seam CreateStateSeam() => new()
    {
        Id = "test-state-001",
        Name = "TestApp/state-contract",
        Type = SeamType.StateContract,
        Provider = CreateProvider(),
        Consumers = Array.Empty<Workspace>(),
        Confidence = 0.85,
        ContractSurface = CreateStateContractSurface()
    };

    public static Seam CreateEmptySeam() => new()
    {
        Id = "test-empty-001",
        Name = "TestApp/empty",
        Type = SeamType.SharedLibrary,
        Provider = new Workspace
        {
            Path = "/app",
            Alias = "TestApp",
            Role = WorkspaceRole.Framework,
            Type = WorkspaceType.AngularCli
        },
        Consumers = Array.Empty<Workspace>(),
        Confidence = 0.5,
        ContractSurface = new ContractSurface()
    };

    private static Workspace CreateProvider() => new()
    {
        Path = "/app/provider",
        Alias = "TestApp",
        Role = WorkspaceRole.Framework,
        Type = WorkspaceType.AngularCli,
        Projects = new[]
        {
            new Project
            {
                Name = "core",
                Type = ProjectType.Library,
                SourceRoot = "/app/provider/projects/core/src"
            },
            new Project
            {
                Name = "shell",
                Type = ProjectType.Application,
                SourceRoot = "/app/provider/projects/shell/src"
            }
        }
    };

    private static Workspace CreateConsumer(string name) => new()
    {
        Path = $"/app/consumers/{name.ToLower()}",
        Alias = name,
        Role = WorkspaceRole.Plugin,
        Type = WorkspaceType.AngularCli,
        Projects = new[]
        {
            new Project
            {
                Name = name.ToLower(),
                Type = ProjectType.Library,
                SourceRoot = $"/app/consumers/{name.ToLower()}/src"
            }
        }
    };

    private static ContractSurface CreateRichContractSurface() => new()
    {
        Elements = new ContractElement[]
        {
            new() { Name = "ITileDescriptor", Kind = ContractElementKind.Interface, SourceFile = "tile.ts", Workspace = "TestApp" },
            new() { Name = "ITileDescriptor.render", Kind = ContractElementKind.Method, SourceFile = "tile.ts", Workspace = "TestApp", ParentName = "ITileDescriptor", TypeSignature = "void" },
            new() { Name = "IPluginConfig", Kind = ContractElementKind.Interface, SourceFile = "plugin.ts", Workspace = "TestApp" },
            new() { Name = "TILE_TOKEN", Kind = ContractElementKind.InjectionToken, SourceFile = "tokens.ts", Workspace = "TestApp", TypeSignature = "ITileDescriptor[]" },
            new() { Name = "CONFIG_TOKEN", Kind = ContractElementKind.InjectionToken, SourceFile = "tokens.ts", Workspace = "TestApp", TypeSignature = "IPluginConfig" },
            new() { Name = "AbstractTile", Kind = ContractElementKind.AbstractClass, SourceFile = "abstract-tile.ts", Workspace = "TestApp", TypeSignature = "implements ITileDescriptor" },
            new() { Name = "config", Kind = ContractElementKind.InputBinding, SourceFile = "tile.component.ts", Workspace = "TestApp", ParentName = "TileComponent", TypeSignature = "IPluginConfig" },
            new() { Name = "tileData", Kind = ContractElementKind.InputBinding, SourceFile = "tile.component.ts", Workspace = "TestApp", ParentName = "TileComponent", TypeSignature = "any" },
            new() { Name = "closed", Kind = ContractElementKind.OutputBinding, SourceFile = "tile.component.ts", Workspace = "TestApp", ParentName = "TileComponent", TypeSignature = "EventEmitter<void>" },
            new() { Name = "TileType", Kind = ContractElementKind.Enum, SourceFile = "models.ts", Workspace = "TestApp" },
            new() { Name = "TileConfig", Kind = ContractElementKind.Type, SourceFile = "models.ts", Workspace = "TestApp" },
            new() { Name = "TileConfig.width", Kind = ContractElementKind.Property, SourceFile = "models.ts", Workspace = "TestApp", ParentName = "TileConfig", TypeSignature = "number" },
            new() { Name = "TileConfig.height", Kind = ContractElementKind.Property, SourceFile = "models.ts", Workspace = "TestApp", ParentName = "TileConfig", TypeSignature = "number" },
            new() { Name = "QueryService", Kind = ContractElementKind.Interface, SourceFile = "query.service.ts", Workspace = "TestApp" },
            new() { Name = "QueryService.query", Kind = ContractElementKind.Method, SourceFile = "query.service.ts", Workspace = "TestApp", ParentName = "QueryService", TypeSignature = "Observable<any>" },
            new() { Name = "CommandService", Kind = ContractElementKind.Interface, SourceFile = "command.service.ts", Workspace = "TestApp" },
            new() { Name = "CommandService.execute", Kind = ContractElementKind.Method, SourceFile = "command.service.ts", Workspace = "TestApp", ParentName = "CommandService", TypeSignature = "Observable<CommandResponse>" },
            new() { Name = "data$", Kind = ContractElementKind.Observable, SourceFile = "data.service.ts", Workspace = "TestApp", ParentName = "DataService", TypeSignature = "Observable<TileData>" },
            new() { Name = "status$", Kind = ContractElementKind.Observable, SourceFile = "data.service.ts", Workspace = "TestApp", ParentName = "DataService", TypeSignature = "Observable<string>" },
            new() { Name = "loadTiles", Kind = ContractElementKind.Action, SourceFile = "tile.actions.ts", Workspace = "TestApp" },
            new() { Name = "tilesLoaded", Kind = ContractElementKind.Action, SourceFile = "tile.actions.ts", Workspace = "TestApp" },
            new() { Name = "selectAllTiles", Kind = ContractElementKind.Selector, SourceFile = "tile.selectors.ts", Workspace = "TestApp", TypeSignature = "ITileDescriptor[]" },
        }
    };

    private static ContractSurface CreateHttpContractSurface() => new()
    {
        Elements = new ContractElement[]
        {
            new() { Name = "RequestService", Kind = ContractElementKind.Interface, SourceFile = "request.service.ts", Workspace = "TestApp" },
            new() { Name = "RequestService.send", Kind = ContractElementKind.Method, SourceFile = "request.service.ts", Workspace = "TestApp", ParentName = "RequestService", TypeSignature = "Observable<RequestResponse>" },
            new() { Name = "RequestService.get", Kind = ContractElementKind.Method, SourceFile = "request.service.ts", Workspace = "TestApp", ParentName = "RequestService", TypeSignature = "Observable<any>" },
            new() { Name = "RequestMessage", Kind = ContractElementKind.Type, SourceFile = "models.ts", Workspace = "TestApp" },
            new() { Name = "RequestResponse", Kind = ContractElementKind.Type, SourceFile = "models.ts", Workspace = "TestApp" },
            new() { Name = "ErrorResponseCode", Kind = ContractElementKind.Enum, SourceFile = "models.ts", Workspace = "TestApp" },
            new() { Name = "API_BASE_URL", Kind = ContractElementKind.InjectionToken, SourceFile = "tokens.ts", Workspace = "TestApp", TypeSignature = "string" },
        }
    };

    private static ContractSurface CreateStateContractSurface() => new()
    {
        Elements = new ContractElement[]
        {
            new() { Name = "AppState", Kind = ContractElementKind.Interface, SourceFile = "state.ts", Workspace = "TestApp" },
            new() { Name = "AppState.tiles", Kind = ContractElementKind.Property, SourceFile = "state.ts", Workspace = "TestApp", ParentName = "AppState", TypeSignature = "ITileDescriptor[]" },
            new() { Name = "AppState.loading", Kind = ContractElementKind.Property, SourceFile = "state.ts", Workspace = "TestApp", ParentName = "AppState", TypeSignature = "boolean" },
            new() { Name = "loadTiles", Kind = ContractElementKind.Action, SourceFile = "actions.ts", Workspace = "TestApp" },
            new() { Name = "selectTiles", Kind = ContractElementKind.Selector, SourceFile = "selectors.ts", Workspace = "TestApp", TypeSignature = "ITileDescriptor[]" },
            new() { Name = "state$", Kind = ContractElementKind.Observable, SourceFile = "store.ts", Workspace = "TestApp", ParentName = "Store", TypeSignature = "Observable<AppState>" },
        }
    };
}
