namespace SeamQ.Core.Models;

public record DiagramSpec
{
    public required string Title { get; init; }
    public DiagramType Type { get; init; }
    public required string SeamId { get; init; }
    public IReadOnlyList<string> Participants { get; init; } = [];
}

public enum DiagramType
{
    ClassApiSurface, ClassBackendContracts, ClassBackendControllers,
    ClassDatastoreSchema, ClassDomainDataObjects, ClassFrontendServices,
    ClassMessageInterfaces, ClassRealtimeCommunication, ClassTelemetryModels,
    ClassTelemetryService, ClassRegistrationSystem, ClassFileStorage,
    SeqAppStartup, SeqPluginLifecycle, SeqDataConsumption,
    SeqTileAddSubscribe, SeqTileRemoveUnsubscribe, SeqRequestFlow,
    SeqQueryFlow, SeqCommandFlow, SeqCommandResponseUi,
    SeqConfigurationCrud, SeqAdvisoryMessage, SeqTelemetrySubscribe,
    SeqErrorHandling, SeqMessageBusRouting, SeqReviewTelemetry,
    StateDatastore, StateSubscriptionLifecycle,
    C4SystemContext, C4ContextWithinArchitecture, C4Container,
    C4ComponentServices, C4ComponentBackend, C4PluginApiLayers,
    C4PluginArchitecture, C4DataFlow, C4SubscriptionChannelMap,
    C4ProtocolStack, C4Dynamic, C4Deployment
}
