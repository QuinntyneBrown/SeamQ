using FluentAssertions;
using SeamQ.Renderer.PlantUml.ClassDiagrams;

namespace SeamQ.Tests.Unit.Renderer;

public class ClassDiagramTests
{
    [Fact]
    public void ApiSurfaceClassDiagram_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = ApiSurfaceClassDiagram.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("ITileDescriptor");
        result.Should().Contain("TILE_TOKEN");
        result.Should().Contain("TileType");
    }

    [Fact]
    public void FrontendServicesClassDiagram_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = FrontendServicesClassDiagram.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Frontend Services");
        result.Should().Contain("QueryService");
        result.Should().Contain("CommandService");
    }

    [Fact]
    public void DomainDataObjectsClassDiagram_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = DomainDataObjectsClassDiagram.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Domain Data Objects");
        result.Should().Contain("TileConfig");
        result.Should().Contain("TileType");
    }

    [Fact]
    public void RegistrationSystemClassDiagram_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = RegistrationSystemClassDiagram.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Registration System");
        result.Should().Contain("TILE_TOKEN");
        result.Should().Contain("CONFIG_TOKEN");
    }

    [Fact]
    public void MessageInterfacesClassDiagram_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = MessageInterfacesClassDiagram.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Message Interfaces");
        result.Should().Contain("loadTiles");
    }

    [Fact]
    public void RealtimeCommunicationClassDiagram_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = RealtimeCommunicationClassDiagram.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Realtime Communication");
        result.Should().Contain("data$");
    }

    [Fact]
    public void AllClassDiagrams_HandleEmptySeam()
    {
        var seam = SeamTestFactory.CreateEmptySeam();

        ApiSurfaceClassDiagram.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        FrontendServicesClassDiagram.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        DomainDataObjectsClassDiagram.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        RegistrationSystemClassDiagram.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        MessageInterfacesClassDiagram.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        RealtimeCommunicationClassDiagram.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
    }
}
