using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class PointSourceProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.PointSource,
        "User-defined point source",
        "Generates one ray from the source origin to each hole point using a user-defined source frame.");

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

        var sourceFrame = PointSourceFrameBuilder.Build(
            parameters.BeamOrigin,
            parameters.SourceFrameX,
            parameters.SourceFrameY);

        var pointSource = parameters.PointSourceOrigin;
        var rays = new List<ProjectionRay>(request.HolePoints.Count);

        foreach (var holePoint in request.HolePoints)
        {
            var direction = new Vector3(
                (float)(holePoint.X - pointSource.X),
                (float)(holePoint.Y - pointSource.Y),
                (float)(holePoint.Z - pointSource.Z));

            if (direction.LengthSquared() <= 0f)
            {
                throw new ArgumentException(
                    $"Hole point ({holePoint.X:F4}, {holePoint.Y:F4}, {holePoint.Z:F4}) coincides with the source origin.",
                    nameof(request));
            }

            var ray = new Ray3D(
                new Vector3((float)pointSource.X, (float)pointSource.Y, (float)pointSource.Z),
                direction);

            rays.Add(new ProjectionRay(ray, holePoint));
        }

        return new ProjectionComputationResult
        {
            MethodId = Metadata.Id,
            PointSourceOrigin = pointSource,
            SourceFrame = sourceFrame,
            Rays = rays,
        };
    }
}
