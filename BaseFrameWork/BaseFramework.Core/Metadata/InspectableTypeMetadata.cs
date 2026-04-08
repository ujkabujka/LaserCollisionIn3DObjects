namespace BaseFramework.Core.Metadata;

public sealed record InspectableTypeMetadata(
    Type SourceType,
    IReadOnlyList<InspectableMemberMetadata> Members);
