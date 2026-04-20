using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Generation;

/// <summary>
/// Generates rays from the curved surface of a cylindrical light source.
/// </summary>
public sealed class CylindricalRayGenerator
{
    /// <summary>
    /// Generates deterministic rays whose origins form a cylindrical shell.
    /// </summary>
    public List<Ray3D> Generate(CylindricalLightSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var rays = new List<Ray3D>(source.RayCount);

        var rows = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(source.RayCount)));
        var columns = Math.Max(1, (int)MathF.Ceiling(source.RayCount / (float)rows));

        for (var i = 0; i < source.RayCount; i++)
        {
            var row = i / columns;
            var column = i % columns;

            var x = source.Height * ((float)row  / rows);
            var theta = 2f * MathF.PI * ((column + 0.5f * (row % 2)) / columns);

            var localOrigin = new Vector3(
                x,
                source.Radius * MathF.Cos(theta),
                source.Radius * MathF.Sin(theta)
                );

            var localDirection = GetRadialDirection(localOrigin);
            var worldOrigin = source.Frame.TransformPointToWorld(localOrigin);
            var worldDirection = Vector3.Normalize(source.Frame.TransformDirectionToWorld(localDirection));

            rays.Add(new Ray3D(worldOrigin, worldDirection));
        }

        return rays;
    }

    private static Vector3 GetRadialDirection(Vector3 localOrigin)
    {
        return Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z));
    }
}
