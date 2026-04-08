using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Collision;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

/// <summary>
/// Represents an oriented rectangular prism in 3D.
/// </summary>
public sealed class RectangularPrism : IRayIntersectable
{
    private const float ParallelEpsilon = 1e-6f;
    private const float DistanceEpsilon = 1e-5f;

    private float _sizeX;
    private float _sizeY;
    private float _sizeZ;

    /// <summary>
    /// Initializes a new instance of the <see cref="RectangularPrism"/> class.
    /// </summary>
    /// <param name="name">Logical object name.</param>
    /// <param name="frame">Frame defining object position and orientation.</param>
    /// <param name="sizeX">Size along local X axis.</param>
    /// <param name="sizeY">Size along local Y axis.</param>
    /// <param name="sizeZ">Size along local Z axis.</param>
    /// <exception cref="ArgumentException">Thrown when any size is not strictly positive.</exception>
    public RectangularPrism(string name, Frame3D frame, float sizeX, float sizeY, float sizeZ)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
    }

    /// <summary>
    /// Gets the object name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the object frame.
    /// </summary>
    public Frame3D Frame { get; }

    /// <summary>
    /// Gets or sets prism size along local X axis.
    /// </summary>
    public float SizeX
    {
        get => _sizeX;
        set => _sizeX = EnsurePositive(value, nameof(SizeX));
    }

    /// <summary>
    /// Gets or sets prism size along local Y axis.
    /// </summary>
    public float SizeY
    {
        get => _sizeY;
        set => _sizeY = EnsurePositive(value, nameof(SizeY));
    }

    /// <summary>
    /// Gets or sets prism size along local Z axis.
    /// </summary>
    public float SizeZ
    {
        get => _sizeZ;
        set => _sizeZ = EnsurePositive(value, nameof(SizeZ));
    }

    /// <summary>
    /// Gets half extents in local space.
    /// </summary>
    public Vector3 HalfExtents => new(SizeX * 0.5f, SizeY * 0.5f, SizeZ * 0.5f);

    /// <summary>
    /// Gets local-space minimum corner.
    /// </summary>
    public Vector3 LocalMin => -HalfExtents;

    /// <summary>
    /// Gets local-space maximum corner.
    /// </summary>
    public Vector3 LocalMax => HalfExtents;

    /// <summary>
    /// Intersects a world-space ray with this oriented prism and returns the first valid hit.
    /// </summary>
    /// <param name="ray">Ray in world space.</param>
    /// <returns>The first hit if found; otherwise <see cref="RayHitResult.NoHit"/>.</returns>
    public RayHitResult Intersect(Ray3D ray)
    {
        ArgumentNullException.ThrowIfNull(ray);

        var localRayOrigin = Frame.TransformPointToLocal(ray.Origin);
        var localRayDirection = Frame.TransformDirectionToLocal(ray.Direction);

        if (!RayBoxIntersection.TryIntersect(
                localRayOrigin,
                localRayDirection,
                LocalMin,
                LocalMax,
                ParallelEpsilon,
                DistanceEpsilon,
                out var localDistance,
                out var localNormal))
        {
            return RayHitResult.NoHit;
        }

        var localHitPoint = localRayOrigin + (localRayDirection * localDistance);
        var worldHitPoint = Frame.TransformPointToWorld(localHitPoint);
        var worldHitNormal = Vector3.Normalize(Frame.TransformDirectionToWorld(localNormal));
        var worldDistance = Vector3.Distance(ray.Origin, worldHitPoint);

        return RayHitResult.Hit(worldDistance, worldHitPoint, worldHitNormal, this);
    }

    private static float EnsurePositive(float value, string paramName)
    {
        if (value <= 0f)
        {
            throw new ArgumentException("Size must be greater than zero.", paramName);
        }

        return value;
    }
}
