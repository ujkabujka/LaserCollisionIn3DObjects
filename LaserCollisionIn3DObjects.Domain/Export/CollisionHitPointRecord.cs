using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Export;

public enum CollisionRaySourceType
{
    CylindricalGenerated,
    ConicalFrustumGenerated,
    CircularOgiveGenerated,
    HybridAxisymmetricGenerated,
    Manual,
    ProjectionResult,
    CompletedProjectionResult,
    ImportedLightSource,
}

public sealed record CollisionHitPointRecord(
    string SceneName,
    Vector3 HitPoint,
    CollisionRaySourceType SourceType,
    string SourceName = "");
