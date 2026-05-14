namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class NamedProjectionResultState
{
    public required string Key { get; init; }

    public required string DisplayName { get; set; }

    public required ProjectionComputationResult Result { get; init; }
}
