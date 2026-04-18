using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class PointSourceProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.PointSource,
        "User-defined point source",
        "Generates one ray from the provided source point to each hole point.");

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Parameters is not PointSourceProjectionParameters parameters)
        {
            throw new ArgumentException("Point-source projection requires point-source parameters.", nameof(request));
        }

        if (request.HolePoints is null || request.HolePoints.Count == 0)
        {
            throw new ArgumentException("Projection requires at least one hole point.", nameof(request));
        }

        var source = parameters.SourcePoint;
        var rays = new List<ProjectionRay>(request.HolePoints.Count);

        foreach (var holePoint in request.HolePoints)
        {
            var direction = new Vector3(
                (float)(holePoint.X - source.X),
                (float)(holePoint.Y - source.Y),
                (float)(holePoint.Z - source.Z));

            if (direction.LengthSquared() <= 0f)
            {
                throw new ArgumentException(
                    $"Hole point ({holePoint.X:F4}, {holePoint.Y:F4}, {holePoint.Z:F4}) coincides with the source point.",
                    nameof(request));
            }

            var ray = new Ray3D(
                new Vector3((float)source.X, (float)source.Y, (float)source.Z),
                direction);

            rays.Add(new ProjectionRay(ray, holePoint));
        }

        return new ProjectionComputationResult
        {
            MethodId = Metadata.Id,
            PointLightSource = new PointLightSource(source),
            Rays = rays,
        };
    }
}
