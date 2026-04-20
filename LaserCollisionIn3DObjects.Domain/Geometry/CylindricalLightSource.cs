using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

/// <summary>
/// Represents a cylindrical light source that emits rays radially outward from its curved surface.
/// </summary>
public sealed class CylindricalLightSource
{
    private float _radius;
    private float _height;
    private int _rayCount;
    private float _tiltWeight = 0.1f;

    public CylindricalLightSource(
        string name,
        Frame3D frame,
        float radius,
        float height,
        int rayCount,
        float tiltWeight = 0.1f)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Radius = radius;
        Height = height;
        RayCount = rayCount;
        TiltWeight = tiltWeight;
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

    public float TiltWeight
    {
        get => _tiltWeight;
        set => _tiltWeight = EnsureNonNegative(value, nameof(TiltWeight));
    }

    private static float EnsurePositive(float value, string paramName)
    {
        if (value <= 0f)
        {
            throw new ArgumentException("Value must be greater than zero.", paramName);
        }

        return value;
    }

    private static float EnsureNonNegative(float value, string paramName)
    {
        if (value < 0f)
        {
            throw new ArgumentException("Value must be greater than or equal to zero.", paramName);
        }

        return value;
    }
}
