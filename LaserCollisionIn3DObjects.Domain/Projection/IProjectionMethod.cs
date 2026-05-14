namespace LaserCollisionIn3DObjects.Domain.Projection;

public interface IProjectionMethod
{
    ProjectionMethodMetadata Metadata { get; }

    ProjectionComputationResult Execute(ProjectionRequest request);
}
