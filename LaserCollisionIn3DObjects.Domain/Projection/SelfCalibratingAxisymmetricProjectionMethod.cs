using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class SelfCalibratingAxisymmetricProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(ProjectionMethodIds.SelfCalibratingAxisymmetricSource, "Self-calibrating axisymmetric inverse projection", "Fits axisymmetric source points and tilt.");
    private readonly SelfCalibratingAxisymmetricProjectionSolver _solver = new();

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        var p = (SelfCalibratingAxisymmetricProjectionParameters)request.Parameters;
        var profile = p.ProfileDefinition.BuildProfile();
        var frame = PointSourceFrameBuilder.Build(p.SourceFrameOrigin, p.SourceFrameX, p.SourceFrameY);
        var worldHolePoints = request.HolePoints;
        var localHolePoints = worldHolePoints.Select(hole => PointSourceFrameTransforms.WorldToLocal(hole, frame)).ToList();
        var result = _solver.Solve(localHolePoints, frame, profile, p.LocalTiltPoint, worldHolePoints, request.Progress);
        return new ProjectionComputationResult { MethodId = Metadata.Id, SourceFrame = frame, Rays = Array.Empty<ProjectionRay>(), AxisymmetricSource = new AxisymmetricProjectionState { SourceFrame = frame, ProfileDefinition = p.ProfileDefinition, Radius = profile.RadiusAt(0), Length = profile.Length, LocalTiltPoint = p.LocalTiltPoint, EstimatedTiltWeight = result.EstimatedTiltWeight, Diagnostics = result.Diagnostics, Points = result.Points } };
    }
}
