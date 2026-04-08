using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

/// <summary>
/// Represents a cylindrical light source that emits multiple rays.
/// </summary>
public sealed class CylindricalLightSource
{
    private float _radius;
    private float _height;
    private int _rayCount;
    private Vector3 _localEmissionDirection;

    public CylindricalLightSource(
        string name,
        Frame3D frame,
        float radius,
        float height,
        int rayCount,
        Vector3 localEmissionDirection)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Radius = radius;
        Height = height;
        RayCount = rayCount;
        LocalEmissionDirection = localEmissionDirection;
    }

    public string Name { get; }

    public Frame3D Frame { get; }

    public float Radius
    {
        get => _radius;
        set => _radius = EnsurePositive(value, nameof(Radius));
    }

    public float Height
    {
        get => _height;
        set => _height = EnsurePositive(value, nameof(Height));
    }

    public int RayCount
    {
        get => _rayCount;
        set => _rayCount = value <= 0 ? throw new ArgumentException("RayCount must be greater than zero.", nameof(RayCount)) : value;
    }

    public Vector3 LocalEmissionDirection
    {
        get => _localEmissionDirection;
        set
        {
            if (value.LengthSquared() <= 0f)
            {
                throw new ArgumentException("Local emission direction must be non-zero.", nameof(LocalEmissionDirection));
            }

            _localEmissionDirection = Vector3.Normalize(value);
        }
    }

    private static float EnsurePositive(float value, string paramName)
    {
        if (value <= 0f)
        {
            throw new ArgumentException("Value must be greater than zero.", paramName);
        }

        return value;
    }
}
