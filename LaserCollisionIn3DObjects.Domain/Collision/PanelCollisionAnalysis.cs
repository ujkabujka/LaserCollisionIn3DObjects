using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Collision;

public sealed record PanelCollisionInputHit(
    int RayIndex,
    RayHitResult Hit,
    CollisionRaySourceType SourceType,
    string SourceName);

public sealed record PanelCollisionHit(
    int PanelHitIndex,
    int RayIndex,
    string PanelName,
    float LocalY,
    float LocalZ,
    Vector3 WorldHitPoint,
    float Distance,
    CollisionRaySourceType SourceType,
    string SourceName);

public sealed record PanelCollisionResult(
    string PanelName,
    int PanelIndex,
    float SizeY,
    float SizeZ,
    IReadOnlyList<PanelCollisionHit> Hits)
{
    public int HitCount => Hits.Count;
}

public sealed record PanelCollisionAnalysis(
    string SceneName,
    int TotalRaysTested,
    IReadOnlyList<PanelCollisionResult> Panels)
{
    public int TotalHits => Panels.Sum(panel => panel.HitCount);
    public int PanelsHit => Panels.Count(panel => panel.HitCount > 0);
}

public sealed class PanelCollisionAnalysisService
{
    public PanelCollisionAnalysis Build(
        string sceneName,
        IReadOnlyList<RectangularPrism> panels,
        IReadOnlyList<PanelCollisionInputHit> hitResults,
        int totalRaysTested)
    {
        ArgumentNullException.ThrowIfNull(sceneName);
        ArgumentNullException.ThrowIfNull(panels);
        ArgumentNullException.ThrowIfNull(hitResults);

        var indexedPanels = panels.Select((panel, index) => (panel, index)).ToArray();
        var grouped = hitResults
            .Where(input => input.Hit.HasHit && input.Hit.HitObject is not null)
            .GroupBy(input => input.Hit.HitObject!, ReferenceEqualityComparer.Instance)
            .ToDictionary(group => group.Key!, group => group.OrderBy(input => input.RayIndex).ToArray());

        var results = new List<PanelCollisionResult>(panels.Count);
        foreach (var (panel, panelIndex) in indexedPanels)
        {
            grouped.TryGetValue(panel, out var panelHits);
            panelHits ??= Array.Empty<PanelCollisionInputHit>();
            var hits = panelHits.Select((input, index) =>
            {
                var local = panel.Frame.TransformPointToLocal(input.Hit.HitPoint);
                return new PanelCollisionHit(index + 1, input.RayIndex, panel.Name, local.Y, local.Z,
                    input.Hit.HitPoint, input.Hit.Distance, input.SourceType, input.SourceName);
            }).ToArray();
            results.Add(new PanelCollisionResult(panel.Name, panelIndex, panel.SizeY, panel.SizeZ, hits));
        }

        return new PanelCollisionAnalysis(sceneName, totalRaysTested, results);
    }
}
