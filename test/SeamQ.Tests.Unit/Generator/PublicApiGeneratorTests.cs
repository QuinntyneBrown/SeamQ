using FluentAssertions;
using SeamQ.Core.Models;
using SeamQ.Generator;

namespace SeamQ.Tests.Unit.Generator;

public class PublicApiGeneratorTests
{
    private readonly PublicApiGenerator _generator = new();

    [Fact]
    public async Task GenerateAsync_CreatesFilePerProject()
    {
        var workspace = CreateWorkspaceWithTwoProjects();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _generator.GenerateAsync(workspace, outputDir);

            files.Should().HaveCount(2);
            files.Should().AllSatisfy(f => File.Exists(f).Should().BeTrue());
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task GenerateAsync_SkipsProjectsWithNoExports()
    {
        var workspace = new Workspace
        {
            Path = "/app",
            Alias = "TestApp",
            Projects = new[]
            {
                new Project
                {
                    Name = "empty-lib",
                    Type = ProjectType.Library,
                    SourceRoot = "/app/libs/empty/src",
                    Exports = []
                },
                CreateLibraryProject("core-lib")
            }
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _generator.GenerateAsync(workspace, outputDir);

            files.Should().HaveCount(1);
            Path.GetFileName(files[0]).Should().Be("core-lib-public-api.md");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task GenerateAsync_ReturnsEmptyWhenNoProjects()
    {
        var workspace = new Workspace
        {
            Path = "/app",
            Alias = "TestApp",
            Projects = []
        };
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-test-{Guid.NewGuid():N}");

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
    public void GenerateProjectMarkdown_ContainsTitle()
    {
        var project = CreateLibraryProject("shared-ui");

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().StartWith("# shared-ui Public API");
    }

    [Fact]
    public void GenerateProjectMarkdown_ContainsOverviewSection()
    {
        var project = CreateLibraryProject("shared-ui");

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("## Overview");
        markdown.Should().Contain("**shared-ui** library");
    }

    [Fact]
    public void GenerateProjectMarkdown_ApplicationType_SaysApplication()
    {
        var project = new Project
        {
            Name = "shell",
            Type = ProjectType.Application,
            SourceRoot = "/app/src",
            Exports = new[]
            {
                new ExportedSymbol { Name = "AppComponent", FilePath = "app.component.ts", Kind = "class" }
            }
        };

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("**shell** application");
    }

    [Fact]
    public void GenerateProjectMarkdown_ContainsSummaryTable()
    {
        var project = CreateLibraryProject("shared-ui");

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("### Summary");
        markdown.Should().Contain("| Category | Count |");
    }

    [Fact]
    public void GenerateProjectMarkdown_GroupsExportsByKind()
    {
        var project = new Project
        {
            Name = "test-lib",
            Type = ProjectType.Library,
            SourceRoot = "/app/libs/test/src",
            Exports = new[]
            {
                new ExportedSymbol { Name = "MyInterface", FilePath = "a.ts", Kind = "interface" },
                new ExportedSymbol { Name = "MyClass", FilePath = "b.ts", Kind = "class" },
                new ExportedSymbol { Name = "MyEnum", FilePath = "c.ts", Kind = "enum" },
                new ExportedSymbol { Name = "MyType", FilePath = "d.ts", Kind = "typealias" }
            }
        };

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("## Classes");
        markdown.Should().Contain("## Interfaces");
        markdown.Should().Contain("## Enumerations");
        markdown.Should().Contain("## Type Aliases");
    }

    [Fact]
    public void GenerateProjectMarkdown_IncludesDocumentation()
    {
        var project = new Project
        {
            Name = "documented-lib",
            Type = ProjectType.Library,
            SourceRoot = "/app/libs/doc/src",
            Exports = new[]
            {
                new ExportedSymbol
                {
                    Name = "UserService",
                    FilePath = "user.service.ts",
                    Kind = "class",
                    Documentation = "Service for managing user accounts and authentication."
                }
            }
        };

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("Service for managing user accounts and authentication.");
    }

    [Fact]
    public void GenerateProjectMarkdown_IncludesTypeSignature()
    {
        var project = new Project
        {
            Name = "typed-lib",
            Type = ProjectType.Library,
            SourceRoot = "/app/libs/typed/src",
            Exports = new[]
            {
                new ExportedSymbol
                {
                    Name = "API_TOKEN",
                    FilePath = "tokens.ts",
                    Kind = "injectiontoken",
                    TypeSignature = "InjectionToken<string>"
                }
            }
        };

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("`InjectionToken<string>`");
    }

    [Fact]
    public void GenerateProjectMarkdown_IncludesParentName()
    {
        var project = new Project
        {
            Name = "parent-lib",
            Type = ProjectType.Library,
            SourceRoot = "/app/libs/parent/src",
            Exports = new[]
            {
                new ExportedSymbol
                {
                    Name = "execute",
                    FilePath = "command.service.ts",
                    Kind = "method",
                    ParentName = "CommandService",
                    TypeSignature = "Observable<Response>"
                }
            }
        };

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("`CommandService`");
    }

    [Fact]
    public void GenerateProjectMarkdown_IncludesSourceFileLocation()
    {
        var project = new Project
        {
            Name = "loc-lib",
            Type = ProjectType.Library,
            SourceRoot = "/app/libs/loc/src",
            Exports = new[]
            {
                new ExportedSymbol
                {
                    Name = "MyClass",
                    FilePath = "my-class.ts",
                    Kind = "class",
                    LineNumber = 42
                }
            }
        };

        var markdown = PublicApiGenerator.GenerateProjectMarkdown(project);

        markdown.Should().Contain("`my-class.ts:42`");
    }

    private static Workspace CreateWorkspaceWithTwoProjects() => new()
    {
        Path = "/app",
        Alias = "TestApp",
        Projects = new[]
        {
            CreateLibraryProject("core-lib"),
            CreateLibraryProject("shared-ui")
        }
    };

    private static Project CreateLibraryProject(string name) => new()
    {
        Name = name,
        Type = ProjectType.Library,
        SourceRoot = $"/app/libs/{name}/src",
        Exports = new[]
        {
            new ExportedSymbol
            {
                Name = "TestInterface",
                FilePath = "test.ts",
                Kind = "interface",
                Documentation = "A test interface."
            },
            new ExportedSymbol
            {
                Name = "TestClass",
                FilePath = "test.ts",
                Kind = "class"
            },
            new ExportedSymbol
            {
                Name = "TestEnum",
                FilePath = "models.ts",
                Kind = "enum"
            }
        }
    };
}
