using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Export;

public enum CollisionRaySourceType
{
    CylindricalGenerated,
    Manual,
    ProjectionResult,
}

public sealed record CollisionHitPointRecord(
    string SceneName,
    Vector3 HitPoint,
    CollisionRaySourceType SourceType);
