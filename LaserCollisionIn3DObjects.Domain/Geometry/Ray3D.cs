using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

/// <summary>
/// Represents a 3D ray defined by an origin and a normalized direction.
/// </summary>
public sealed class Ray3D
{
    private Vector3 _direction;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ray3D"/> class.
    /// </summary>
    /// <param name="origin">Ray origin in world space.</param>
    /// <param name="direction">Ray direction. The value is normalized on assignment.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="direction"/> has zero length.</exception>
    public Ray3D(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = direction;
    }

    /// <summary>
    /// Gets or sets the ray origin in world space.
    /// </summary>
    public Vector3 Origin { get; set; }

    /// <summary>
    /// Gets or sets the normalized ray direction.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the assigned direction has zero length.</exception>
    public Vector3 Direction
    {
        get => _direction;
        set => _direction = NormalizeOrThrow(value);
    }

    /// <summary>
    /// Gets a point located along the ray at the supplied distance.
    /// </summary>
    /// <param name="distance">Distance from the origin in units.</param>
    /// <returns>The world-space point at the given distance.</returns>
    public Vector3 GetPoint(float distance)
    {
        return Origin + (Direction * distance);
    }

    private static Vector3 NormalizeOrThrow(Vector3 value)
    {
        if (value.LengthSquared() <= 0f)
        {
            throw new ArgumentException("Direction must be non-zero.", nameof(value));
        }

        return Vector3.Normalize(value);
    }
}
