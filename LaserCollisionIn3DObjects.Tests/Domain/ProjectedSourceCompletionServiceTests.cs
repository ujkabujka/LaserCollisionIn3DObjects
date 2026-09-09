using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.SourceCompletion;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class ProjectedSourceCompletionServiceTests
{
    private static readonly AxisymmetricSourceProfileDefinition CylinderProfile = new()
    {
        Kind = AxisymmetricSourceKind.Cylinder,
        Radius = 2f,
        Length = 10f,
    };

    [Fact]
    public void ProjectedSourceIdentity_IsStableAcrossRecreatedInputs()
    {
        var beforeRefresh = new ProjectedSourceIdentity("Projection Scene", "result-b");
        var afterRefresh = new ProjectedSourceIdentity("Projection Scene", "result-b");

        Assert.Equal(beforeRefresh, afterRefresh);
        Assert.Equal(beforeRefresh.ToString(), afterRefresh.ToString());
    }

    [Fact]
    public void ProjectedSourceIdentity_DistinguishesResultKeysAndOwningScenes()
    {
        var selected = new ProjectedSourceIdentity("Scene A", "result-b");

        Assert.NotEqual(selected, new ProjectedSourceIdentity("Scene A", "result-a"));
        Assert.NotEqual(selected, new ProjectedSourceIdentity("Scene B", "result-b"));
    }

    [Fact]
    public void DetectsTwoCoverageIntervalsAndExpectedGaps()
    {
        var request = BuildRequest("Coverage", CreateRaysAtAngles(new[] { 60d, 70d, 80d, 90d, 210d, 220d, 230d, 240d }));
        var analyzer = new ProjectedSourceAzimuthAnalyzer();

        var coverage = analyzer.DetectCoverage(request, 15d);
        var gaps = analyzer.DetectGaps(request, 15d);

        Assert.Equal(2, coverage.Count);
        Assert.Contains(gaps, gap => Approximately(gap.StartDegrees, 0d, 1e-3) && Approximately(gap.EndDegrees, 60d, 1e-3));
        Assert.Contains(gaps, gap => Approximately(gap.StartDegrees, 90d, 1e-3) && Approximately(gap.EndDegrees, 210d, 1e-3));
        Assert.Contains(gaps, gap => Approximately(gap.StartDegrees, 240d, 1e-3) && Approximately(gap.EndDegrees, 360d, 1e-3));
    }

    [Fact]
    public void RotationalCopyFillsGaps()
    {
        var request = BuildRequest("Complete", CreateRaysAtAngles(new[] { 60d, 70d, 80d, 90d, 210d, 220d, 230d, 240d }));
        var service = new ProjectedSourceCompletionService();

        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: true));

        Assert.True(result.Rays.Count > request.Rays.Count);
        Assert.True(result.SyntheticRayCount > 0);
    }

    [Fact]
    public void RotationalCopy_FillsLargeGapWithRepeatedSlices()
    {
        var request = BuildRequest("LargeGap", CreateRaysAtAngles(new[] { 0d, 20d, 40d, 60d, 80d, 100d }));
        var service = new ProjectedSourceCompletionService();

        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(10d, 25d, IncludeOriginalRays: false));

        var syntheticThetas = result.Rays
            .Select(ray => NormalizeDegrees(ComputeThetaDegrees(ToLocal(ray.Ray.Origin, request.SourceFrame))))
            .OrderBy(v => v)
            .ToList();

        Assert.NotEmpty(syntheticThetas);
        Assert.Contains(syntheticThetas, t => t >= 139.9d && t <= 160.1d);
        Assert.Contains(syntheticThetas, t => t >= 259.9d && t <= 280.1d);
    }

    [Fact]
    public void RotationalCopy_RotatesBothOriginAndDirection()
    {
        var request = BuildRequest("RotateBoth", CreateRaysAtAngles(new[] { 0d, 20d, 40d, 60d, 80d, 100d }));
        var service = new ProjectedSourceCompletionService();

        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(20d, 25d, IncludeOriginalRays: false));
        var syntheticRay = result.Rays.First();

        var originTheta = NormalizeDegrees(ComputeThetaDegrees(ToLocal(syntheticRay.Ray.Origin, request.SourceFrame)));
        var directionTheta = NormalizeDegrees(ComputeThetaDegrees(ToLocalDirection(syntheticRay.Ray.Direction, request.SourceFrame)));
        Assert.False(float.IsNaN((float)originTheta));
        Assert.False(float.IsNaN((float)directionTheta));
        Assert.InRange(syntheticRay.Ray.Direction.Length(), 0.9999f, 1.0001f);
    }

    [Fact]
    public void SyntheticOriginsLieOnCylinderProfile()
    {
        var request = BuildRequest("Cylinder", CreateRaysAtAngles(new[] { 60d, 90d, 210d, 240d }));
        var service = new ProjectedSourceCompletionService();

        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: false));

        foreach (var ray in result.Rays)
        {
            var local = ToLocal(ray.Ray.Origin, request.SourceFrame);
            var radial = Math.Sqrt((local.Y * local.Y) + (local.Z * local.Z));
            Assert.InRange(radial, 1.99d, 2.01d);
            Assert.InRange(local.X, 0d, 10d);
        }
    }

    [Fact]
    public void NonIdentityFrameStillProducesValidProfileOrigins()
    {
        var frame = new PointSourceFrameState
        {
            Origin = new Point3(10, -3, 5),
            AxisX = new Vector3D(0, 1, 0),
            AxisY = new Vector3D(0, 0, 1),
            AxisZ = new Vector3D(1, 0, 0),
        };

        var request = BuildRequest("Rotated", CreateRaysAtAngles(new[] { 60d, 90d, 210d, 240d }, frame), frame);
        var service = new ProjectedSourceCompletionService();

        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: false));

        foreach (var ray in result.Rays)
        {
            var local = ToLocal(ray.Ray.Origin, frame);
            var radial = Math.Sqrt((local.Y * local.Y) + (local.Z * local.Z));
            Assert.InRange(radial, 1.99d, 2.01d);
        }
    }

    [Fact]
    public void SyntheticDirectionsAreNormalized()
    {
        var request = BuildRequest("Directions", CreateRaysAtAngles(new[] { 60d, 90d, 210d, 240d }));
        var service = new ProjectedSourceCompletionService();

        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: false));

        foreach (var ray in result.Rays)
        {
            Assert.InRange(ray.Ray.Direction.Length(), 0.9999f, 1.0001f);
        }
    }

    [Fact]
    public void PreservesOriginalRaysWhenIncluded()
    {
        var original = CreateRaysAtAngles(new[] { 60d, 90d, 210d, 240d });
        var request = BuildRequest("Original", original);
        var service = new ProjectedSourceCompletionService();

        var before = original.Select(r => (r.Ray.Origin, r.Ray.Direction)).ToArray();
        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: true));

        Assert.True(result.Rays.Count >= original.Count);
        for (var i = 0; i < before.Length; i++)
        {
            Assert.Equal(before[i].Origin, original[i].Ray.Origin);
            Assert.Equal(before[i].Direction, original[i].Ray.Direction);
        }
    }

    [Fact]
    public void WorksForConicalFrustum()
    {
        var profile = new AxisymmetricSourceProfileDefinition
        {
            Kind = AxisymmetricSourceKind.ConicalFrustum,
            RadiusStart = 3f,
            RadiusEnd = 1f,
            Length = 12f,
        };

        var request = new ProjectedSourceCompletionRequest("Conical", profile, IdentityFrame(), CreateRaysAtAngles(new[] { 60d, 90d, 210d, 240d }, IdentityFrame(), profile));
        var service = new ProjectedSourceCompletionService();

        var result = service.CompleteByRotationalCopy(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: false));
        var builtProfile = profile.BuildProfile();

        foreach (var ray in result.Rays)
        {
            var local = ToLocal(ray.Ray.Origin, request.SourceFrame);
            var radial = Math.Sqrt((local.Y * local.Y) + (local.Z * local.Z));
            var expected = builtProfile.RadiusAt((float)Math.Clamp(local.X, 0d, builtProfile.Length));
            Assert.InRange(radial, expected - 0.05d, expected + 0.05d);
        }
    }

    [Fact]
    public void EmptyInputThrows()
    {
        var request = BuildRequest("Empty", new List<ProjectionRay>());
        var service = new ProjectedSourceCompletionService();

        var ex = Assert.Throws<ArgumentException>(() =>
            service.CompleteByRotationalCopy(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: true)));

        Assert.Contains("at least one ray", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompleteDispatch_RotationalCopy_MatchesDirectCall()
    {
        var request = BuildRequest("Dispatch", CreateRaysAtAngles(new[] { 60d, 90d, 210d, 240d }));
        var service = new ProjectedSourceCompletionService();
        var settings = new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: true, Method: SourceCompletionMethod.RotationalCopy);

        var viaDispatch = service.Complete(request, settings);
        var direct = service.CompleteByRotationalCopy(request, settings);

        Assert.Equal(direct.SyntheticRayCount, viaDispatch.SyntheticRayCount);
        Assert.Equal(direct.Rays.Count, viaDispatch.Rays.Count);
    }

    [Fact]
    public void MirrorCompletionCreatesSyntheticRays()
    {
        var request = BuildRequest("Mirror", CreateRaysAtAngles(new[] { 60d, 70d, 80d, 90d }));
        var service = new ProjectedSourceCompletionService();

        var result = service.Complete(request, new SourceCompletionSettings(30d, 15d, IncludeOriginalRays: false, Method: SourceCompletionMethod.Mirror, MirrorAxisDegrees: 0d));

        Assert.True(result.SyntheticRayCount > 0);
        foreach (var ray in result.Rays)
        {
            Assert.InRange(ray.Ray.Direction.Length(), 0.9999f, 1.0001f);
        }
    }

    [Fact]
    public void Mirror_DirectionsRemainNormalized()
    {
        var request = BuildRequest("MirrorNorm", CreateRaysAtAngles(new[] { 20d, 40d, 60d, 80d, 100d }));
        var service = new ProjectedSourceCompletionService();
        var result = service.Complete(request, new SourceCompletionSettings(10d, 15d, IncludeOriginalRays: false, Method: SourceCompletionMethod.Mirror));
        Assert.All(result.Rays, r => Assert.InRange(r.Ray.Direction.Length(), 0.9999f, 1.0001f));
    }

    private static ProjectedSourceCompletionRequest BuildRequest(string name, IReadOnlyList<ProjectionRay> rays, PointSourceFrameState? frame = null)
        => new(name, CylinderProfile, frame ?? IdentityFrame(), rays);

    private static PointSourceFrameState IdentityFrame() => new()
    {
        Origin = new Point3(0, 0, 0),
        AxisX = new Vector3D(1, 0, 0),
        AxisY = new Vector3D(0, 1, 0),
        AxisZ = new Vector3D(0, 0, 1),
    };

    private static List<ProjectionRay> CreateRaysAtAngles(IEnumerable<double> angles, PointSourceFrameState? frame = null, AxisymmetricSourceProfileDefinition? profileDefinition = null)
    {
        var effectiveFrame = frame ?? IdentityFrame();
        var profile = (profileDefinition ?? CylinderProfile).BuildProfile();
        const float u = 5f;

        var list = new List<ProjectionRay>();
        foreach (var angle in angles)
        {
            var theta = (float)(angle * Math.PI / 180d);
            var localOrigin = profile.EvaluateSurfacePoint(u, theta);
            var localDirection = Vector3.Normalize(new Vector3(1f, 0.2f, -0.1f));
            var worldOrigin = ToWorld(localOrigin, effectiveFrame);
            var worldDirection = ToWorldDirection(localDirection, effectiveFrame);
            var ray = new Ray3D(worldOrigin, worldDirection);
            var target = ray.GetPoint(1000f);
            list.Add(new ProjectionRay(ray, new Point3(target.X, target.Y, target.Z)));
        }

        return list;
    }

    private static Vector3 ToWorld(Vector3 local, PointSourceFrameState frame)
    {
        var origin = new Vector3((float)frame.Origin.X, (float)frame.Origin.Y, (float)frame.Origin.Z);
        var x = new Vector3((float)frame.AxisX.X, (float)frame.AxisX.Y, (float)frame.AxisX.Z);
        var y = new Vector3((float)frame.AxisY.X, (float)frame.AxisY.Y, (float)frame.AxisY.Z);
        var z = new Vector3((float)frame.AxisZ.X, (float)frame.AxisZ.Y, (float)frame.AxisZ.Z);
        return origin + (local.X * x) + (local.Y * y) + (local.Z * z);
    }

    private static Vector3 ToWorldDirection(Vector3 local, PointSourceFrameState frame)
    {
        var x = new Vector3((float)frame.AxisX.X, (float)frame.AxisX.Y, (float)frame.AxisX.Z);
        var y = new Vector3((float)frame.AxisY.X, (float)frame.AxisY.Y, (float)frame.AxisY.Z);
        var z = new Vector3((float)frame.AxisZ.X, (float)frame.AxisZ.Y, (float)frame.AxisZ.Z);
        return Vector3.Normalize((local.X * x) + (local.Y * y) + (local.Z * z));
    }

    private static Vector3 ToLocal(Vector3 world, PointSourceFrameState frame)
    {
        var origin = new Vector3((float)frame.Origin.X, (float)frame.Origin.Y, (float)frame.Origin.Z);
        var delta = world - origin;
        var x = new Vector3((float)frame.AxisX.X, (float)frame.AxisX.Y, (float)frame.AxisX.Z);
        var y = new Vector3((float)frame.AxisY.X, (float)frame.AxisY.Y, (float)frame.AxisY.Z);
        var z = new Vector3((float)frame.AxisZ.X, (float)frame.AxisZ.Y, (float)frame.AxisZ.Z);
        return new Vector3(Vector3.Dot(delta, x), Vector3.Dot(delta, y), Vector3.Dot(delta, z));
    }

    private static Vector3 ToLocalDirection(Vector3 worldDirection, PointSourceFrameState frame)
    {
        var x = new Vector3((float)frame.AxisX.X, (float)frame.AxisX.Y, (float)frame.AxisX.Z);
        var y = new Vector3((float)frame.AxisY.X, (float)frame.AxisY.Y, (float)frame.AxisY.Z);
        var z = new Vector3((float)frame.AxisZ.X, (float)frame.AxisZ.Y, (float)frame.AxisZ.Z);
        return Vector3.Normalize(new Vector3(Vector3.Dot(worldDirection, x), Vector3.Dot(worldDirection, y), Vector3.Dot(worldDirection, z)));
    }

    private static bool Approximately(double value, double expected, double tolerance) => Math.Abs(value - expected) <= tolerance;
    private static double ComputeThetaDegrees(Vector3 localPoint) => Math.Atan2(localPoint.Z, localPoint.Y) * (180d / Math.PI);
    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }
}
