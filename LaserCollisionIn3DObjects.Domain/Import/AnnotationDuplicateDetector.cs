namespace LaserCollisionIn3DObjects.Domain.Import;

/// <summary>Tracks exact point-annotation identities and compact per-category import counts.</summary>
public sealed class AnnotationDuplicateDetector
{
    private readonly HashSet<AnnotationGeometryKey> _seen = new();

    public int RawHoleCount { get; private set; }
    public int RawNaturalCount { get; private set; }
    public int RemovedHoleCount { get; private set; }
    public int RemovedNaturalCount { get; private set; }
    public int RemovedCount => RemovedHoleCount + RemovedNaturalCount;

    public bool TryAccept(AnnotationGeometryKey key)
    {
        switch (key.Category)
        {
            case AnnotationSemanticType.Hole: RawHoleCount++; break;
            case AnnotationSemanticType.Natural: RawNaturalCount++; break;
            default: throw new ArgumentException("Only Hole and Natural annotations can be deduplicated.", nameof(key));
        }

        if (_seen.Add(key)) return true;
        if (key.Category == AnnotationSemanticType.Hole) RemovedHoleCount++;
        else RemovedNaturalCount++;
        return false;
    }

    public IEnumerable<string> CreateDiagnostics()
    {
        if (RemovedHoleCount > 0) yield return $"Removed {RemovedHoleCount} duplicate Hole annotations.";
        if (RemovedNaturalCount > 0) yield return $"Removed {RemovedNaturalCount} duplicate Natural annotations.";
    }
}
