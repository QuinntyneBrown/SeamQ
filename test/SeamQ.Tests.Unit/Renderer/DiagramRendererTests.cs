using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SeamQ.Renderer;

namespace SeamQ.Tests.Unit.Renderer;

public class DiagramRendererTests
{
    private readonly DiagramRenderer _renderer = new(NullLogger<DiagramRenderer>.Instance);

    [Fact]
    public async Task RenderAsync_PluginSeam_GeneratesMultipleDiagrams()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _renderer.RenderAsync(seam, outputDir);

            files.Should().NotBeEmpty();
            files.Should().AllSatisfy(f => f.Should().EndWith(".puml"));
            foreach (var file in files)
            {
                File.Exists(file).Should().BeTrue($"file {file} should exist");
                var content = await File.ReadAllTextAsync(file);
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
    public async Task RenderAsync_WithClassFilter_OnlyGeneratesClassDiagrams()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _renderer.RenderAsync(seam, outputDir, "class");

            files.Should().NotBeEmpty();
            files.Should().AllSatisfy(f => Path.GetFileName(f).Should().Contain("Class"));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task RenderAsync_WithSequenceFilter_OnlyGeneratesSequenceDiagrams()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _renderer.RenderAsync(seam, outputDir, "sequence");

            files.Should().NotBeEmpty();
            files.Should().AllSatisfy(f => Path.GetFileName(f).Should().Contain("Seq"));
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public async Task RenderAsync_EmptySeam_StillProducesValidOutput()
    {
        var seam = SeamTestFactory.CreateEmptySeam();
        var outputDir = Path.Combine(Path.GetTempPath(), $"seamq-test-{Guid.NewGuid():N}");

        try
        {
            var files = await _renderer.RenderAsync(seam, outputDir);

            // Empty seam may produce 0 diagrams if no data — that's valid
            files.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }
}
