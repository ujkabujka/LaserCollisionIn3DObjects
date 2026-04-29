using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class SelfCalibratingAxisymmetricProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.SelfCalibratingCylindricalSource,
        "Self-calibrating axisymmetric inverse projection",
        "Fits source-surface points on a cylinder and estimates one global tilt weight from all hole points.");

    private readonly SelfCalibratingAxisymmetricProjectionSolver _solver;

    public SelfCalibratingAxisymmetricProjectionMethod(SelfCalibratingAxisymmetricProjectionSolver? solver = null)
    {
        _solver = solver ?? new SelfCalibratingAxisymmetricProjectionSolver();
    }

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = request.Parameters switch
        {
            SelfCalibratingAxisymmetricProjectionParameters axisymmetric => new SelfCalibratingCylindricalProjectionParameters(
                axisymmetric.SourceFrameOrigin,
                axisymmetric.SourceFrameX,
                axisymmetric.SourceFrameY,
                axisymmetric.Radius,
                axisymmetric.Length,
                axisymmetric.LocalTiltPoint),
            SelfCalibratingCylindricalProjectionParameters cylindrical => cylindrical,
            _ => null,
        };

        if (parameters is null)
        {
            throw new ArgumentException("Self-calibrating axisymmetric projection requires self-calibrating axisymmetric parameters.", nameof(request));
        }

        if (request.HolePoints is null || request.HolePoints.Count == 0)
        {
            throw new ArgumentException("Projection requires at least one hole point.", nameof(request));
        }

        var profile = parameters.ProfileDefinition.Profile;
        var radius = profile.RadiusAt(0f);
        var length = profile.Length;

        if (radius <= 0d)
        {
            throw new ArgumentException("Cylinder radius must be greater than zero.", nameof(request));
        }

        if (length <= 0d)
        {
            throw new ArgumentException("Cylinder length must be greater than zero.", nameof(request));
        }

        var sourceFrame = PointSourceFrameBuilder.Build(parameters.SourceFrameOrigin, parameters.SourceFrameX, parameters.SourceFrameY);
        var localHolePoints = request.HolePoints.Select(h => ToLocal(h, sourceFrame)).ToList();

        var solveResult = _solver.Solve(
            localHolePoints,
            sourceFrame,
            radius,
            length,
            parameters.LocalTiltPoint,
            request.HolePoints,
            request.Progress);

        return new ProjectionComputationResult
        {
            MethodId = Metadata.Id,
            SourceFrame = sourceFrame,
            Rays = Array.Empty<ProjectionRay>(),
            AxisymmetricSource = new AxisymmetricProjectionState
            {
                SourceFrame = sourceFrame,
                Radius = radius,
                Length = length,
                LocalTiltPoint = parameters.LocalTiltPoint,
                EstimatedTiltWeight = solveResult.EstimatedTiltWeight,
                Diagnostics = solveResult.Diagnostics,
                Points = solveResult.Points,
            },
        };
    }

    private static Point3 ToLocal(Point3 worldPoint, PointSourceFrameState frame)
    {
        var deltaX = worldPoint.X - frame.Origin.X;
        var deltaY = worldPoint.Y - frame.Origin.Y;
        var deltaZ = worldPoint.Z - frame.Origin.Z;

        return new Point3(
            Dot(deltaX, deltaY, deltaZ, frame.AxisX),
            Dot(deltaX, deltaY, deltaZ, frame.AxisY),
            Dot(deltaX, deltaY, deltaZ, frame.AxisZ));
    }

    private static double Dot(double dx, double dy, double dz, Vector3D axis)
    {
        return (dx * axis.X) + (dy * axis.Y) + (dz * axis.Z);
    }
}
