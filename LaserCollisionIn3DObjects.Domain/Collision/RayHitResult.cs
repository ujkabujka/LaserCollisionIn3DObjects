using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Collision;

/// <summary>
/// Represents the result of a ray intersection query.
/// </summary>
public sealed class RayHitResult
{
    private RayHitResult(bool hasHit, float distance, Vector3 hitPoint, Vector3 hitNormal, RectangularPrism? hitObject)
    {
        HasHit = hasHit;
        Distance = distance;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        HitObject = hitObject;
    }

    /// <summary>
    /// Gets a value indicating whether an intersection occurred.
    /// </summary>
    public bool HasHit { get; }

    /// <summary>
    /// Gets the distance from ray origin to intersection.
    /// </summary>
    public float Distance { get; }

    /// <summary>
    /// Gets the world-space hit point.
    /// </summary>
    public Vector3 HitPoint { get; }

    /// <summary>
    /// Gets the world-space hit normal.
    /// </summary>
    public Vector3 HitNormal { get; }

    /// <summary>
    /// Gets the object hit by the ray.
    /// </summary>
    public RectangularPrism? HitObject { get; }

    /// <summary>
    /// Gets a singleton no-hit result.
    /// </summary>
    public static RayHitResult NoHit { get; } = new(false, float.PositiveInfinity, Vector3.Zero, Vector3.Zero, null);

    /// <summary>
    /// Creates a hit result with the supplied intersection data.
    /// </summary>
    /// <param name="distance">Distance from ray origin.</param>
    /// <param name="hitPoint">World-space intersection point.</param>
    /// <param name="hitNormal">World-space intersection normal.</param>
    /// <param name="hitObject">Intersected object.</param>
    /// <returns>A populated hit result.</returns>
    public static RayHitResult Hit(float distance, Vector3 hitPoint, Vector3 hitNormal, RectangularPrism? hitObject)
    {
        return new RayHitResult(true, distance, hitPoint, hitNormal, hitObject);
    }
}
