using FluentAssertions;
using SeamQ.Renderer.PlantUml.SequenceDiagrams;

namespace SeamQ.Tests.Unit.Renderer;

public class SequenceDiagramTests
{
    [Fact]
    public void PluginLifecycleSequence_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = PluginLifecycleSequence.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Plugin Lifecycle");
        result.Should().Contain("PluginA");
        result.Should().Contain("PluginB");
    }

    [Fact]
    public void AppStartupSequence_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = AppStartupSequence.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("App Startup");
        result.Should().Contain("Bootstrap");
    }

    [Fact]
    public void RequestFlowSequence_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreateHttpApiSeam();
        var result = RequestFlowSequence.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Request Flow");
    }

    [Fact]
    public void QueryFlowSequence_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = QueryFlowSequence.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Query Flow");
    }

    [Fact]
    public void CommandFlowSequence_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = CommandFlowSequence.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Command Flow");
    }

    [Fact]
    public void DataConsumptionSequence_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = DataConsumptionSequence.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Data Consumption");
    }

    [Fact]
    public void ErrorHandlingSequence_GeneratesValidPlantUml()
    {
        var seam = SeamTestFactory.CreatePluginSeam();
        var result = ErrorHandlingSequence.Generate(seam);

        result.Should().StartWith("@startuml");
        result.Should().EndWith("@enduml\r\n");
        result.Should().Contain("Error Handling");
    }

    [Fact]
    public void AllSequenceDiagrams_HandleEmptySeam()
    {
        var seam = SeamTestFactory.CreateEmptySeam();

        AppStartupSequence.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        RequestFlowSequence.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        QueryFlowSequence.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        CommandFlowSequence.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        DataConsumptionSequence.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
        ErrorHandlingSequence.Generate(seam).Should().Contain("@startuml").And.Contain("@enduml");
    }
}
