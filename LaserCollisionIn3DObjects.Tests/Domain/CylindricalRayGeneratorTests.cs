using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class CylindricalRayGeneratorTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Generate_ReturnsConfiguredRayCount()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource("S", new Frame3D(), 2f, 4f, 150);

        var rays = generator.Generate(source);

        Assert.Equal(150, rays.Count);
    }

    [Fact]
    public void Generate_RayOriginsStayOnCylindricalShell()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource("S", new Frame3D(), 3f, 6f, 40, tiltWeight: 0.1f);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var radialDistance = MathF.Sqrt((ray.Origin.Y * ray.Origin.Y) + (ray.Origin.Z * ray.Origin.Z));
            Assert.Equal(3f, radialDistance, 3);
            Assert.InRange(ray.Origin.X, 0f, 6f);
        }
    }

    [Fact]
    public void Generate_WithZeroTiltWeight_KeepsPureRadialDirectionsInSourceLocalSpace()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(4f, -2f, 7f),
            Quaternion.CreateFromYawPitchRoll(0.4f, -0.2f, 0.3f));
        var source = new CylindricalLightSource("S", frame, 2.5f, 8f, 48, tiltWeight: 0f, tiltPointLocal: new Vector3(25f, -12f, 3f));

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var localOrigin = frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(frame.TransformDirectionToLocal(ray.Direction));
            var expectedLocalDirection = Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z));

            AssertVectorEqual(expectedLocalDirection, localDirection);
            Assert.Equal(0f, localDirection.X, Tolerance);
        }
    }

    [Fact]
    public void Generate_WithDefaultTiltPoint_UsesRayOriginAsUnnormalizedTiltVector()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(-11f, 8f, 2f),
            Quaternion.CreateFromYawPitchRoll(-0.9f, 0.3f, 1.1f));
        var source = new CylindricalLightSource("S", frame, 1.2f, 5f, 63, tiltWeight: 0.1f);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var localOrigin = frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(frame.TransformDirectionToLocal(ray.Direction));
            var radial = Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z));
            var expectedLocalDirection = Vector3.Normalize(radial + (0.1f * localOrigin));

            AssertVectorEqual(expectedLocalDirection, localDirection);
        }
    }

    [Fact]
    public void Generate_WithNonZeroTiltPoint_UsesRayOriginMinusTiltPoint()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(3f, -2f, 5f),
            Quaternion.CreateFromYawPitchRoll(0.3f, 0.6f, -0.4f));
        var tiltPointLocal = new Vector3(4f, 0f, 0f);
        var source = new CylindricalLightSource("S", frame, 2f, 8f, 36, tiltWeight: 0.2f, tiltPointLocal: tiltPointLocal);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var localOrigin = frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(frame.TransformDirectionToLocal(ray.Direction));
            var radial = Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z));
            var tiltVector = localOrigin - tiltPointLocal;
            var expectedLocalDirection = Vector3.Normalize(radial + (0.2f * tiltVector));

            AssertVectorEqual(expectedLocalDirection, localDirection);
        }
    }

    [Fact]
    public void Generate_WithZeroTiltVectorForARay_FallsBackToRadialWithoutCrashing()
    {
        var generator = new CylindricalRayGenerator();
        var tiltPointLocal = new Vector3(0f, 1f, 0f);
        var source = new CylindricalLightSource("S", new Frame3D(), 1f, 3f, 8, tiltWeight: 0.5f, tiltPointLocal: tiltPointLocal);

        var rays = generator.Generate(source);

        var matchingRay = rays.Single(ray =>
        {
            var localOrigin = source.Frame.TransformPointToLocal(ray.Origin);
            return Vector3.Distance(localOrigin, tiltPointLocal) < 1e-5f;
        });

        var localDirection = Vector3.Normalize(source.Frame.TransformDirectionToLocal(matchingRay.Direction));
        var localOriginForMatch = source.Frame.TransformPointToLocal(matchingRay.Origin);
        var expectedRadial = Vector3.Normalize(new Vector3(0f, localOriginForMatch.Y, localOriginForMatch.Z));

        AssertVectorEqual(expectedRadial, localDirection);
    }

    [Fact]
    public void Generate_UsesUnnormalizedTiltVectorInBlend()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource(
            "S",
            new Frame3D(),
            radius: 1f,
            height: 3f,
            rayCount: 8,
            tiltWeight: 0.4f,
            tiltPointLocal: new Vector3(-10f, 0f, 0f));

        var rays = generator.Generate(source);
        var sampleRay = rays[0];
        var localOrigin = source.Frame.TransformPointToLocal(sampleRay.Origin);
        var radial = Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z));
        var tiltVector = localOrigin - source.TiltPointLocal;
        var localDirection = Vector3.Normalize(source.Frame.TransformDirectionToLocal(sampleRay.Direction));

        var expectedUsingUnnormalizedTilt = Vector3.Normalize(radial + (source.TiltWeight * tiltVector));
        var expectedIfTiltWereNormalized = Vector3.Normalize(radial + (source.TiltWeight * Vector3.Normalize(tiltVector)));

        AssertVectorEqual(expectedUsingUnnormalizedTilt, localDirection);
        Assert.True(Vector3.Distance(expectedUsingUnnormalizedTilt, expectedIfTiltWereNormalized) > 0.01f);
    }

    [Fact]
    public void Generate_ChangingTiltWeightDoesNotChangeRayOrigins()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(3f, 4f, 5f),
            Quaternion.CreateFromYawPitchRoll(0.2f, 0.3f, -0.1f));

        var zeroTiltSource = new CylindricalLightSource("S", frame, 3f, 6f, 49, tiltWeight: 0f, tiltPointLocal: new Vector3(100f, 0f, 0f));
        var nonZeroTiltSource = new CylindricalLightSource("S", frame, 3f, 6f, 49, tiltWeight: 0.1f, tiltPointLocal: new Vector3(100f, 0f, 0f));

        var zeroTiltRays = generator.Generate(zeroTiltSource);
        var nonZeroTiltRays = generator.Generate(nonZeroTiltSource);

        Assert.Equal(zeroTiltRays.Count, nonZeroTiltRays.Count);
        for (var i = 0; i < zeroTiltRays.Count; i++)
        {
            AssertVectorEqual(zeroTiltRays[i].Origin, nonZeroTiltRays[i].Origin);
        }
    }

    [Fact]
    public void Generate_RotatedSource_InterpretsTiltPointInLocalFrame()
    {
        var generator = new CylindricalRayGenerator();
        var orientation = Quaternion.CreateFromYawPitchRoll(0.5f, -0.3f, 0.8f);
        var source = new CylindricalLightSource(
            "S",
            new Frame3D(new Vector3(10f, -4f, 2f), orientation),
            radius: 1.5f,
            height: 4f,
            rayCount: 24,
            tiltWeight: 0.15f,
            tiltPointLocal: new Vector3(4f, 0f, 0f));

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var localOrigin = source.Frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(source.Frame.TransformDirectionToLocal(ray.Direction));
            var expectedLocal = Vector3.Normalize(
                Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z)) +
                (0.15f * (localOrigin - source.TiltPointLocal)));

            AssertVectorEqual(expectedLocal, localDirection);
        }
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
