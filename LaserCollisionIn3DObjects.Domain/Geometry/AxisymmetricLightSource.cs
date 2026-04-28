using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public class AxisymmetricLightSource
{
    private int _rayCount;
    private float _tiltWeight;

    public AxisymmetricLightSource(
        string name,
        Frame3D frame,
        AxisymmetricSourceKind sourceKind,
        IAxisymmetricSourceProfile profile,
        int rayCount,
        float tiltWeight = 0.1f,
        Vector3? tiltPointLocal = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        SourceKind = sourceKind;
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        RayCount = rayCount;
        TiltWeight = tiltWeight;
        TiltPointLocal = tiltPointLocal ?? Vector3.Zero;
    }

    public string Name { get; }
    public Frame3D Frame { get; }
    public AxisymmetricSourceKind SourceKind { get; }
    public IAxisymmetricSourceProfile Profile { get; }

    public int RayCount
    {
        get => _rayCount;
        set => _rayCount = value <= 0 ? throw new ArgumentException("RayCount must be greater than zero.", nameof(RayCount)) : value;
    }

    public float TiltWeight
    {
        get => _tiltWeight;
        set => _tiltWeight = value < 0f ? throw new ArgumentException("TiltWeight must be greater than or equal to zero.", nameof(TiltWeight)) : value;
    }

    public Vector3 TiltPointLocal { get; set; }
}
