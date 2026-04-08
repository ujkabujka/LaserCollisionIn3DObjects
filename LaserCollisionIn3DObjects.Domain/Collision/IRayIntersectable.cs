using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Collision;

/// <summary>
/// Defines a contract for objects that can be intersected by a <see cref="Ray3D"/>.
/// </summary>
public interface IRayIntersectable
{
    /// <summary>
    /// Intersects the current object with a ray.
    /// </summary>
    /// <param name="ray">Ray to test in world space.</param>
    /// <returns>Intersection result data.</returns>
    RayHitResult Intersect(Ray3D ray);
}
