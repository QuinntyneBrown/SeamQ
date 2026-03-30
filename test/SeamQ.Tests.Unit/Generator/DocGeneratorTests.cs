using FluentAssertions;
using SeamQ.Core.Models;
using SeamQ.Generator;

namespace SeamQ.Tests.Unit.Generator;

public class DocGeneratorTests
{
    private readonly DocGenerator _generator = new();

    [Fact]
    public async Task GenerateAsync_CreatesReadmePerProject()
    {
        var workspace = CreateWorkspaceWithExports();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-doc-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _generator.GenerateAsync(workspace, outputDir);

            files.Should().Contain(f => f.EndsWith("README.md"));
            var readmeFiles = files.Where(f => f.EndsWith("README.md")).ToList();
            readmeFiles.Should().HaveCount(1);

            var readmeContent = await File.ReadAllTextAsync(readmeFiles[0]);
            readmeContent.Should().NotBeNullOrWhiteSpace();
            readmeContent.Should().Contain("# core-lib API Reference");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task GenerateAsync_CreatesDiagramsFolder()
    {
        var workspace = CreateWorkspaceWithExports();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-doc-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _generator.GenerateAsync(workspace, outputDir);

            var pumlFiles = files.Where(f => f.EndsWith(".puml")).ToList();
            pumlFiles.Should().NotBeEmpty();

            foreach (var pumlFile in pumlFiles)
            {
                pumlFile.Should().Contain("diagrams");
                File.Exists(pumlFile).Should().BeTrue();
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task GenerateAsync_DiagramsContainStartAndEndUml()
    {
        var workspace = CreateWorkspaceWithExports();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-doc-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _generator.GenerateAsync(workspace, outputDir);

            var pumlFiles = files.Where(f => f.EndsWith(".puml")).ToList();
            pumlFiles.Should().NotBeEmpty();

            foreach (var pumlFile in pumlFiles)
            {
                var content = await File.ReadAllTextAsync(pumlFile);
                content.Should().Contain("@startuml");
                content.Should().Contain("@enduml");
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void GetDocumentableSymbols_ExcludesPrivateMembers()
    {
        var exports = new List<ExportedSymbol>
        {
            new() { Name = "PublicClass", FilePath = "a.ts", Kind = "Class" },
            new() { Name = "_privateHelper", FilePath = "a.ts", Kind = "Method", ParentName = "PublicClass" },
            new() { Name = "#internalField", FilePath = "a.ts", Kind = "Property", ParentName = "PublicClass" },
            new() { Name = "publicMethod", FilePath = "a.ts", Kind = "Method", ParentName = "PublicClass" }
        };

        var result = DocGenerator.GetDocumentableSymbols(exports);

        result.Should().Contain(s => s.Name == "PublicClass");
        result.Should().Contain(s => s.Name == "publicMethod");
        result.Should().NotContain(s => s.Name == "_privateHelper");
        result.Should().NotContain(s => s.Name == "#internalField");
    }

    [Fact]
    public void GetDocumentableSymbols_ExcludesDottedPrivateMembers()
    {
        var exports = new List<ExportedSymbol>
        {
            new() { Name = "MyClass", FilePath = "a.ts", Kind = "Class" },
            new() { Name = "MyClass._secret", FilePath = "a.ts", Kind = "Property", ParentName = "MyClass" },
            new() { Name = "MyClass.visible", FilePath = "a.ts", Kind = "Property", ParentName = "MyClass" }
        };

        var result = DocGenerator.GetDocumentableSymbols(exports);

        result.Should().NotContain(s => s.Name == "MyClass._secret");
        result.Should().Contain(s => s.Name == "MyClass.visible");
    }

    [Fact]
    public void GetDocumentableSymbols_ExcludesBarrelExports()
    {
        var exports = new List<ExportedSymbol>
        {
            new() { Name = "MyClass", FilePath = "a.ts", Kind = "Class" },
            new() { Name = "*", FilePath = "index.ts", Kind = "WildcardExport" },
            new() { Name = "MyClass", FilePath = "index.ts", Kind = "NamedExport" },
            new() { Name = "default", FilePath = "index.ts", Kind = "DefaultExport" }
        };

        var result = DocGenerator.GetDocumentableSymbols(exports);

        result.Should().HaveCount(1);
        result[0].Kind.Should().Be("Class");
    }

    [Fact]
    public void GenerateReadme_IncludesJSDocComments()
    {
        var project = new Project
        {
            Name = "doc-lib",
            Type = ProjectType.Library,
            SourceRoot = "/app/libs/doc/src",
            Exports = new[]
            {
                new ExportedSymbol
                {
                    Name = "UserService",
                    FilePath = "user.service.ts",
                    Kind = "Injectable",
                    Documentation = "Manages user authentication and profile data."
                }
            }
        };

        var typeGroups = DocGenerator.BuildTypeGroups(DocGenerator.GetDocumentableSymbols(project.Exports));
        var markdown = DocGenerator.GenerateReadme(project, typeGroups);

        markdown.Should().Contain("Manages user authentication and profile data.");
    }

    [Fact]
    public void GenerateReadme_AutoGeneratesDescriptionWhenJSDocAbsent()
    {
        var project = new Project
        {
            Name = "auto-lib",
            Type = ProjectType.Library,
            SourceRoot = "/app/libs/auto/src",
            Exports = new[]
            {
                new ExportedSymbol
                {
                    Name = "AppConfig",
                    FilePath = "config.ts",
                    Kind = "Interface"
                }
            }
        };

        var typeGroups = DocGenerator.BuildTypeGroups(DocGenerator.GetDocumentableSymbols(project.Exports));
        var markdown = DocGenerator.GenerateReadme(project, typeGroups);

        markdown.Should().Contain("Interface defining the shape of AppConfig");
    }

    [Fact]
    public void GenerateAutoDescription_Component_ReturnsAngularComponent()
    {
        var symbol = new ExportedSymbol
        {
            Name = "DashboardComponent",
            FilePath = "dashboard.component.ts",
            Kind = "Component"
        };

        var desc = DocGenerator.GenerateAutoDescription(symbol);

        desc.Should().Be("Angular component");
    }

    [Fact]
    public void GenerateAutoDescription_Injectable_ReturnsInjectableService()
    {
        var symbol = new ExportedSymbol
        {
            Name = "DataService",
            FilePath = "data.service.ts",
            Kind = "Injectable"
        };

        var desc = DocGenerator.GenerateAutoDescription(symbol);

        desc.Should().Be("Injectable service");
    }

    [Fact]
    public void GenerateAutoDescription_Enum_ReturnsEnumerationDescription()
    {
        var symbol = new ExportedSymbol
        {
            Name = "Status",
            FilePath = "models.ts",
            Kind = "Enum"
        };

        var desc = DocGenerator.GenerateAutoDescription(symbol);

        desc.Should().Be("Enumeration defining Status values");
    }

    [Fact]
    public void GenerateAutoDescription_InjectionToken_IncludesType()
    {
        var symbol = new ExportedSymbol
        {
            Name = "API_URL",
            FilePath = "tokens.ts",
            Kind = "InjectionToken",
            TypeSignature = "string"
        };

        var desc = DocGenerator.GenerateAutoDescription(symbol);

        desc.Should().Be("Injection token of type string");
    }

    [Fact]
    public void GenerateAutoDescription_Property_IncludesType()
    {
        var symbol = new ExportedSymbol
        {
            Name = "name",
            FilePath = "model.ts",
            Kind = "Property",
            TypeSignature = "string"
        };

        var desc = DocGenerator.GenerateAutoDescription(symbol);

        desc.Should().Be("Property of type string");
    }

    [Fact]
    public void GenerateAutoDescription_OutputBinding_ReturnsOutputEventEmitter()
    {
        var symbol = new ExportedSymbol
        {
            Name = "closed",
            FilePath = "dialog.component.ts",
            Kind = "OutputBinding"
        };

        var desc = DocGenerator.GenerateAutoDescription(symbol);

        desc.Should().Be("Output event emitter");
    }

    [Fact]
    public async Task GenerateAsync_EmptyProjectsProduceNoOutput()
    {
        var workspace = new Workspace
        {
            Path = "/app",
            Alias = "EmptyApp",
            Projects = new[]
            {
                new Project
                {
                    Name = "empty-lib",
                    Type = ProjectType.Library,
                    SourceRoot = "/app/libs/empty/src",
                    Exports = Array.Empty<ExportedSymbol>()
                }
            }
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-doc-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _generator.GenerateAsync(workspace, outputDir);

            files.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task GenerateAsync_ProjectWithOnlyBarrelExportsProducesNoOutput()
    {
        var workspace = new Workspace
        {
            Path = "/app",
            Alias = "BarrelApp",
            Projects = new[]
            {
                new Project
                {
                    Name = "barrel-lib",
                    Type = ProjectType.Library,
                    SourceRoot = "/app/libs/barrel/src",
                    Exports = new[]
                    {
                        new ExportedSymbol { Name = "*", FilePath = "index.ts", Kind = "WildcardExport" },
                        new ExportedSymbol { Name = "SomeClass", FilePath = "index.ts", Kind = "NamedExport" }
                    }
                }
            }
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-doc-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _generator.GenerateAsync(workspace, outputDir);

            files.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void GenerateDiagram_ContainsStartAndEndUml()
    {
        var group = new DocGenerator.TypeGroup(
            new ExportedSymbol { Name = "MyClass", FilePath = "a.ts", Kind = "Class" },
            new List<ExportedSymbol>
            {
                new() { Name = "id", FilePath = "a.ts", Kind = "Property", ParentName = "MyClass", TypeSignature = "number" }
            }
        );

        var puml = DocGenerator.GenerateDiagram(group);

        puml.Should().Contain("@startuml");
        puml.Should().Contain("@enduml");
        puml.Should().Contain("class MyClass");
    }

    [Fact]
    public void GenerateDiagram_InterfaceUsesInterfaceKeyword()
    {
        var group = new DocGenerator.TypeGroup(
            new ExportedSymbol { Name = "IConfig", FilePath = "a.ts", Kind = "Interface" },
            new List<ExportedSymbol>()
        );

        var puml = DocGenerator.GenerateDiagram(group);

        puml.Should().Contain("interface IConfig");
    }

    [Fact]
    public void GenerateDiagram_EnumUsesEnumKeyword()
    {
        var group = new DocGenerator.TypeGroup(
            new ExportedSymbol { Name = "Status", FilePath = "a.ts", Kind = "Enum" },
            new List<ExportedSymbol>
            {
                new() { Name = "Active", FilePath = "a.ts", Kind = "Property", ParentName = "Status" },
                new() { Name = "Inactive", FilePath = "a.ts", Kind = "Property", ParentName = "Status" }
            }
        );

        var puml = DocGenerator.GenerateDiagram(group);

        puml.Should().Contain("enum Status");
    }

    [Fact]
    public void BuildTypeGroups_GroupsChildrenWithParent()
    {
        var symbols = new List<ExportedSymbol>
        {
            new() { Name = "UserService", FilePath = "user.service.ts", Kind = "Injectable" },
            new() { Name = "getUser", FilePath = "user.service.ts", Kind = "Method", ParentName = "UserService" },
            new() { Name = "userId", FilePath = "user.service.ts", Kind = "Property", ParentName = "UserService", TypeSignature = "string" }
        };

        var groups = DocGenerator.BuildTypeGroups(symbols);

        groups.Should().HaveCount(1);
        groups[0].Symbol.Name.Should().Be("UserService");
        groups[0].Members.Should().HaveCount(2);
    }

    [Fact]
    public void GetDocumentableSymbols_IncludesInputBindingsRegardlessOfAnnotation()
    {
        var exports = new List<ExportedSymbol>
        {
            new() { Name = "MyComponent", FilePath = "c.ts", Kind = "Component" },
            new()
            {
                Name = "data",
                FilePath = "c.ts",
                Kind = "InputBinding",
                ParentName = "MyComponent",
                TypeSignature = "any"
            },
            new()
            {
                Name = "closed",
                FilePath = "c.ts",
                Kind = "OutputBinding",
                ParentName = "MyComponent",
                TypeSignature = "EventEmitter<void>"
            }
        };

        var result = DocGenerator.GetDocumentableSymbols(exports);

        result.Should().HaveCount(3);
        result.Should().Contain(s => s.Kind == "InputBinding");
        result.Should().Contain(s => s.Kind == "OutputBinding");
    }

    private static Workspace CreateWorkspaceWithExports() => new()
    {
        Path = "/app",
        Alias = "TestApp",
        Projects = new[]
        {
            new Project
            {
                Name = "core-lib",
                Type = ProjectType.Library,
                SourceRoot = "/app/libs/core/src",
                Exports = new[]
                {
                    new ExportedSymbol
                    {
                        Name = "UserService",
                        FilePath = "user.service.ts",
                        Kind = "Injectable",
                        Documentation = "Service for managing users.",
                        LineNumber = 10
                    },
                    new ExportedSymbol
                    {
                        Name = "getUser",
                        FilePath = "user.service.ts",
                        Kind = "Method",
                        ParentName = "UserService",
                        TypeSignature = "Observable<User>"
                    },
                    new ExportedSymbol
                    {
                        Name = "IUser",
                        FilePath = "models.ts",
                        Kind = "Interface",
                        LineNumber = 5
                    },
                    new ExportedSymbol
                    {
                        Name = "IUser.name",
                        FilePath = "models.ts",
                        Kind = "Property",
                        ParentName = "IUser",
                        TypeSignature = "string"
                    },
                    new ExportedSymbol
                    {
                        Name = "UserRole",
                        FilePath = "models.ts",
                        Kind = "Enum",
                        LineNumber = 20
                    }
                }
            }
        }
    };
}
