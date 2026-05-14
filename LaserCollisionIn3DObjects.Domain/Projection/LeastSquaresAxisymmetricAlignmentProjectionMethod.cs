using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresAxisymmetricAlignmentProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource, "Least-squares axisymmetric alignment", "Refines axisymmetric source-surface points.");

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        var p = (LeastSquaresAxisymmetricAlignmentProjectionParameters)request.Parameters;
        var profile = p.ProfileDefinition.BuildProfile();
        var frame = PointSourceFrameBuilder.Build(p.SourceFrameOrigin, p.SourceFrameX, p.SourceFrameY);
        var local = request.HolePoints.Select(h => new Point3(h.X - frame.Origin.X, h.Y - frame.Origin.Y, h.Z - frame.Origin.Z)).ToList();

        var solver = new LeastSquaresAxisymmetricAlignmentSolver(p.SolverSettings);

        var result = solver.Solve(local, frame, profile, p.LocalTiltPoint, request.HolePoints, request.Progress);
        return new ProjectionComputationResult { MethodId = Metadata.Id, SourceFrame = frame, Rays = Array.Empty<ProjectionRay>(), AxisymmetricSource = new AxisymmetricProjectionState { SourceFrame = frame, ProfileDefinition = p.ProfileDefinition, Radius = profile.RadiusAt(0), Length = profile.Length, LocalTiltPoint = p.LocalTiltPoint, EstimatedTiltWeight = result.RefinedLambda, LeastSquaresDiagnostics = result.Diagnostics, Points = result.Points } };
    }
}
