namespace LaserCollisionIn3DObjects.Domain.Export;

public static class CollisionHitPointExportSelector
{
    public static IReadOnlyList<CollisionHitPointRecord> ForCylindricalGeneratedHits(IEnumerable<CollisionHitPointRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records.Where(record => record.SourceType == CollisionRaySourceType.CylindricalGenerated).ToList();
    }
}
