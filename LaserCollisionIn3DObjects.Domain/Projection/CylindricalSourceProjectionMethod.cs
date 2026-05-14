using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class AxisymmetricSourceProjectionMethod : IProjectionMethod
{
    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.AxisymmetricSource,
        "Direct linear projection method",
        "Directly maps local hole coordinates to the selected axisymmetric source geometry using axial normalization and angular projection. Works with cylinder, conical frustum, circular ogive, and hybrid geometries.");

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        var p = (AxisymmetricSourceProjectionParameters)request.Parameters;
        var profile = p.ProfileDefinition.BuildProfile();
        var frame = PointSourceFrameBuilder.Build(p.SourceFrameOrigin, p.SourceFrameX, p.SourceFrameY);

        if (request.HolePoints.Count == 0)
        {
            return new ProjectionComputationResult { MethodId = Metadata.Id, SourceFrame = frame, Rays = Array.Empty<ProjectionRay>(), AxisymmetricSource = new AxisymmetricProjectionState { SourceFrame = frame, ProfileDefinition = p.ProfileDefinition, Radius = profile.RadiusAt(0), Length = profile.Length, Points = [] } };
        }

        var points = DirectAxisymmetricProjectionInitializer.Initialize(request.HolePoints, frame, profile);

        return new ProjectionComputationResult { MethodId = Metadata.Id, SourceFrame = frame, Rays = Array.Empty<ProjectionRay>(), AxisymmetricSource = new AxisymmetricProjectionState { SourceFrame = frame, ProfileDefinition = p.ProfileDefinition, Radius = profile.RadiusAt(0), Length = profile.Length, Points = points } };
    }

}
