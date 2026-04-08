using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class RectangularPrismIntersectionTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void RayHitsCenteredAxisAlignedPrismFromFront()
    {
        var prism = CreateCenteredPrism();
        var ray = new Ray3D(new Vector3(0f, 0f, -5f), Vector3.UnitZ);

        var hit = prism.Intersect(ray);

        Assert.True(hit.HasHit);
        Assert.Equal(4f, hit.Distance, Tolerance);
        AssertVectorEqual(new Vector3(0f, 0f, -1f), hit.HitPoint);
    }

    [Fact]
    public void RayMissesPrismCompletely()
    {
        var prism = CreateCenteredPrism();
        var ray = new Ray3D(new Vector3(0f, 3f, -5f), Vector3.UnitZ);

        var hit = prism.Intersect(ray);

        Assert.False(hit.HasHit);
    }

    [Fact]
    public void RayStartsInsidePrismAndExitsCorrectly()
    {
        var prism = CreateCenteredPrism();
        var ray = new Ray3D(Vector3.Zero, Vector3.UnitX);

        var hit = prism.Intersect(ray);

        Assert.True(hit.HasHit);
        Assert.Equal(1f, hit.Distance, Tolerance);
        AssertVectorEqual(new Vector3(1f, 0f, 0f), hit.HitPoint);
        AssertVectorEqual(Vector3.UnitX, hit.HitNormal);
    }

    [Fact]
    public void RayParallelToFace_MissesWhenOutsideSlab()
    {
        var prism = CreateCenteredPrism();
        var ray = new Ray3D(new Vector3(3f, 0f, -5f), Vector3.UnitZ);

        var hit = prism.Intersect(ray);

        Assert.False(hit.HasHit);
    }

    [Fact]
    public void RayParallelToFace_IntersectsWhenWithinSlab()
    {
        var prism = CreateCenteredPrism();
        var ray = new Ray3D(new Vector3(0.5f, 0f, -5f), Vector3.UnitZ);

        var hit = prism.Intersect(ray);

        Assert.True(hit.HasHit);
        Assert.Equal(4f, hit.Distance, Tolerance);
        AssertVectorEqual(new Vector3(0.5f, 0f, -1f), hit.HitPoint);
    }

    [Fact]
    public void RotatedPrism_StillIntersectsCorrectly()
    {
        var frame = new Frame3D(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f));
        var prism = new RectangularPrism("Rotated", frame, 2f, 2f, 4f);
        var ray = new Ray3D(new Vector3(-5f, 0f, 0f), Vector3.UnitX);

        var hit = prism.Intersect(ray);

        Assert.True(hit.HasHit);
        Assert.Equal(3f, hit.Distance, Tolerance);
        AssertVectorEqual(new Vector3(-2f, 0f, 0f), hit.HitPoint);
    }

    [Fact]
    public void ClosestPositiveIntersection_IsReturned()
    {
        var prism = CreateCenteredPrism();
        var ray = new Ray3D(new Vector3(-5f, 0f, 0f), Vector3.UnitX);

        var hit = prism.Intersect(ray);

        Assert.True(hit.HasHit);
        Assert.Equal(4f, hit.Distance, Tolerance);
        AssertVectorEqual(new Vector3(-1f, 0f, 0f), hit.HitPoint);
    }

    [Fact]
    public void FrontFaceHitNormal_PointsExpectedDirection()
    {
        var prism = CreateCenteredPrism();
        var ray = new Ray3D(new Vector3(0f, 0f, -5f), Vector3.UnitZ);

        var hit = prism.Intersect(ray);

        Assert.True(hit.HasHit);
        AssertVectorEqual(new Vector3(0f, 0f, -1f), hit.HitNormal);
    }

    private static RectangularPrism CreateCenteredPrism()
    {
        return new RectangularPrism("Centered", new Frame3D(Vector3.Zero, Quaternion.Identity), 2f, 2f, 2f);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
