using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresCylindricalAlignmentProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource,
        "Least-squares axisymmetric alignment",
        "Refines axisymmetric source-surface points by minimizing direction-alignment error between modeled axisymmetric rays and source-to-hole directions for cylinder, conical frustum, circular ogive, and hybrid profiles.");

    private readonly LeastSquaresCylindricalAlignmentSolver _solver;

    public LeastSquaresCylindricalAlignmentProjectionMethod(LeastSquaresCylindricalAlignmentSolver? solver = null)
    {
        _solver = solver ?? new LeastSquaresCylindricalAlignmentSolver();
    }

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Parameters is not LeastSquaresAxisymmetricAlignmentProjectionParameters parameters)
        {
            throw new ArgumentException("Least-squares cylindrical alignment projection requires least-squares cylindrical alignment parameters.", nameof(request));
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
        var localHolePoints = request.HolePoints.Select(holePoint => ToLocal(holePoint, sourceFrame)).ToList();

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
            CylindricalSource = new CylindricalProjectionState
            {
                SourceFrame = sourceFrame,
                Radius = radius,
                Length = length,
                LocalTiltPoint = parameters.LocalTiltPoint,
                EstimatedTiltWeight = solveResult.RefinedLambda,
                LeastSquaresDiagnostics = solveResult.Diagnostics,
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
