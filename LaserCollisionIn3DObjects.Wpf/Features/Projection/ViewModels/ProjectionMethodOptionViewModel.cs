using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Wpf.Features.Projection.ViewModels;

public sealed class ProjectionMethodOptionViewModel
{
    public required IProjectionMethod Method { get; init; }

    public string Id => Method.Metadata.Id;

    public string DisplayName => Method.Metadata.DisplayName;

    public string Description => Method.Metadata.Description;
}
