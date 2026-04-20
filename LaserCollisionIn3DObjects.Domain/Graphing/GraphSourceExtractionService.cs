using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

public sealed class GraphSourceExtractionService
{
    private readonly CylindricalRayGenerator _rayGenerator;

    public GraphSourceExtractionService(CylindricalRayGenerator? rayGenerator = null)
    {
        _rayGenerator = rayGenerator ?? new CylindricalRayGenerator();
    }

    public IReadOnlyList<GraphableSourceData> Extract(IReadOnlyList<GraphSceneData> scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        var sources = new List<GraphableSourceData>();

        foreach (var scene in scenes)
        {
            foreach (var source in scene.CylindricalSources)
            {
                var rays = _rayGenerator.Generate(source).ToList();
                sources.Add(new GraphableSourceData
                {
                    Id = $"{scene.SceneName}::cyl::{source.Name}",
                    DisplayName = $"[{scene.SceneName}] Cylindrical Source: {source.Name}",
                    Kind = GraphableSourceKind.CylindricalLightSource,
                    AxisX = source.Frame.TransformDirectionToWorld(Vector3.UnitX),
                    FrameOrigin = source.Frame.Position,
                    SourceLength = source.Height,
                    Rays = rays,
                });
            }

            foreach (var result in scene.ProjectionResults)
            {
                var pointLaserSource = result.Result.ToPointLaserSource();
                var axisX = pointLaserSource.AxisX;
                sources.Add(new GraphableSourceData
                {
                    Id = $"{scene.SceneName}::proj::{result.Key}",
                    DisplayName = $"[{scene.SceneName}] Projection Result: {result.DisplayName}",
                    Kind = GraphableSourceKind.ProjectionResult,
                    AxisX = new Vector3((float)axisX.X, (float)axisX.Y, (float)axisX.Z),
                    FrameOrigin = new Vector3((float)pointLaserSource.Origin.X, (float)pointLaserSource.Origin.Y, (float)pointLaserSource.Origin.Z),
                    SourceLength = null,
                    Rays = pointLaserSource.Rays.Select(projectionRay => projectionRay.Ray).ToList(),
                });
            }
        }

        return sources;
    }
}
