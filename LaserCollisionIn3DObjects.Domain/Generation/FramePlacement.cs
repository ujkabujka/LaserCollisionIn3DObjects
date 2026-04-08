using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Generation;

public readonly record struct FramePlacement(Vector3 Position, Quaternion Orientation);
