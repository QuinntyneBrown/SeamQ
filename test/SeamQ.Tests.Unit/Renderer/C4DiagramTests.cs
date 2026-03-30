using FluentAssertions;
using SeamQ.Renderer.C4;

namespace SeamQ.Tests.Unit.Renderer;

public class C4DiagramTests
{
    [Fact]
    public void C4SystemContext_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = C4SystemContext.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("C4_Context.puml");
        result.Should().Contain("TestApp");
        result.Should().Contain("PluginA");
    }

    [Fact]
    public void C4Container_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = C4Container.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("C4_Container.puml");
        result.Should().Contain("core");
        result.Should().Contain("shell");
    }

    [Fact]
    public void C4ComponentServices_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = C4ComponentServices.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("C4_Component.puml");
        result.Should().Contain("Component");
    }

    [Fact]
    public void C4PluginApiLayers_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = C4PluginApiLayers.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("C4_Component.puml");
        result.Should().Contain("Registration Layer");
        result.Should().Contain("Contract Layer");
    }

    [Fact]
    public void C4Dynamic_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = C4Dynamic.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("C4_Dynamic.puml");
        result.Should().Contain("RelIndex");
    }

    [Fact]
    public void C4DataFlow_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = C4DataFlow.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("C4_Container.puml");
        result.Should().Contain("Data Flow");
    }

    [Fact]
    public void AllC4Diagrams_HandleEmptySeam()
    {
        var seam = SeamTestFactory.CreateEmptySeam();

        C4SystemContext.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        C4Container.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        C4ComponentServices.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        C4PluginApiLayers.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        C4Dynamic.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        C4DataFlow.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
    }
}
