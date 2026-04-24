using System.Linq;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class ProjectionComputationResult
{
    public required string MethodId { get; init; }

    public Point3? PointSourceOrigin { get; init; }

    public required PointSourceFrameState SourceFrame { get; init; }

    public required IReadOnlyList<ProjectionRay> Rays { get; init; }

    public CylindricalProjectionState? CylindricalSource { get; init; }

    public PointLaserSourceState ToPointLaserSource()
    {
        var rays = Rays;
        if (rays.Count == 0 && CylindricalSource is not null)
        {
            rays = CylindricalSource.Points.Select(point => new ProjectionRay(
                new Ray3D(
                    new System.Numerics.Vector3((float)point.RayOrigin.X, (float)point.RayOrigin.Y, (float)point.RayOrigin.Z),
                    new System.Numerics.Vector3((float)point.RayDirection.X, (float)point.RayDirection.Y, (float)point.RayDirection.Z)),
                point.HolePoint)).ToList();
        }

        return new PointLaserSourceState
        {
            Origin = SourceFrame.Origin,
            AxisX = SourceFrame.AxisX,
            AxisY = SourceFrame.AxisY,
            AxisZ = SourceFrame.AxisZ,
            Rays = rays,
        };
    }
}
