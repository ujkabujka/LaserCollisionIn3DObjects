using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class AxisymmetricSourceProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.AxisymmetricSource,
        "User-defined axisymmetric source",
        "Projects hole points to an axisymmetric source profile.");

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        var p = (AxisymmetricSourceProjectionParameters)request.Parameters;
        var profile = p.ProfileDefinition.BuildProfile();
        var frame = PointSourceFrameBuilder.Build(p.SourceFrameOrigin, p.SourceFrameX, p.SourceFrameY);
        var points = request.HolePoints.Select((h, i) =>
        {
            var u = (profile.Length * i) / Math.Max(1, request.HolePoints.Count - 1);
            var theta = 0d;
            var surf = profile.EvaluateSurfacePoint((float)u, (float)theta);
            var src = new Point3(frame.Origin.X + surf.X, frame.Origin.Y + surf.Y, frame.Origin.Z + surf.Z);
            var dir = Vector3.Normalize(new Vector3((float)(h.X - src.X), (float)(h.Y - src.Y), (float)(h.Z - src.Z)));
            return new AxisymmetricProjectionPoint(h, src, new Vector3D(dir.X, dir.Y, dir.Z), src)
            {
                LocalU = u,
                LocalTheta = theta,
                UnwrappedU = u,
                UnwrappedV = profile.RadiusAt((float)u) * theta,
            };
        }).ToList();

        return new ProjectionComputationResult { MethodId = Metadata.Id, SourceFrame = frame, Rays = Array.Empty<ProjectionRay>(), AxisymmetricSource = new AxisymmetricProjectionState { SourceFrame = frame, Radius = profile.RadiusAt(0), Length = profile.Length, Points = points } };
    }
}
