using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class ProjectionPointSelectorTests
{
    private static readonly Point3[] Holes = [new(1, 0, 0), new(2, 0, 0)];
    private static readonly Point3[] Natural = [new(10, 0, 0), new(20, 0, 0), new(30, 0, 0)];

    [Fact]
    public void BuildEffectivePoints_IncludesNaturalPointsWhenEnabled()
        => Assert.Equal(Holes.Concat(Natural), ProjectionPointSelector.BuildEffectivePoints(Holes, Natural, true));

    [Fact]
    public void BuildEffectivePoints_ExcludesNaturalCoordinatesWhenDisabled()
        => Assert.Equal(Holes, ProjectionPointSelector.BuildEffectivePoints(Holes, Natural, false));
}
