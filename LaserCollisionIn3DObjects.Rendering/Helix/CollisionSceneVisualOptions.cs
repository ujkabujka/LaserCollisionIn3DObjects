namespace LaserCollisionIn3DObjects.Rendering.Helix;

/// <summary>Display-only switches applied while constructing collision visuals.</summary>
public sealed record CollisionSceneVisualOptions(
    bool ShowCollisionRays = true,
    bool ShowCollisionHitPoints = true);
