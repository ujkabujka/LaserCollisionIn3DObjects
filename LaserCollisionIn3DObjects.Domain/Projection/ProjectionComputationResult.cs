using System.Linq;
using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class ProjectionComputationResult
{
    public required string MethodId { get; init; }

    public Point3? PointSourceOrigin { get; init; }

    public required PointSourceFrameState SourceFrame { get; init; }

    public required IReadOnlyList<ProjectionRay> Rays { get; init; }

    public CylindricalProjectionState? CylindricalSource { get; init; }

    public IReadOnlyList<ProjectionRay> GetEffectiveRays()
    {
        if (Rays.Count > 0)
        {
            return Rays;
        }

        if (CylindricalSource is null)
        {
            return Array.Empty<ProjectionRay>();
        }

        return CylindricalSource.Points.Select(point => new ProjectionRay(
            new Ray3D(
                new Vector3((float)point.RayOrigin.X, (float)point.RayOrigin.Y, (float)point.RayOrigin.Z),
                new Vector3((float)point.RayDirection.X, (float)point.RayDirection.Y, (float)point.RayDirection.Z)),
            point.HolePoint)).ToList();
    }

    public PointLaserSourceState ToPointLaserSource()
    {
        return new PointLaserSourceState
        {
            Origin = SourceFrame.Origin,
            AxisX = SourceFrame.AxisX,
            AxisY = SourceFrame.AxisY,
            AxisZ = SourceFrame.AxisZ,
            Rays = GetEffectiveRays(),
        };
    }
}
