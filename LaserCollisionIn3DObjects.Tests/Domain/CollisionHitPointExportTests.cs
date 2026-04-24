using System.Globalization;
using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Export;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class CollisionHitPointExportTests
{
    [Fact]
    public void Selector_IncludesOnlyCylindricalGeneratedHits()
    {
        var records = new[]
        {
            new CollisionHitPointRecord("Scene A", new Vector3(1, 2, 3), CollisionRaySourceType.CylindricalGenerated),
            new CollisionHitPointRecord("Scene A", new Vector3(4, 5, 6), CollisionRaySourceType.Manual),
            new CollisionHitPointRecord("Scene A", new Vector3(7, 8, 9), CollisionRaySourceType.ProjectionResult),
            new CollisionHitPointRecord("Scene A", new Vector3(10, 11, 12), CollisionRaySourceType.CylindricalGenerated),
        };

        var filtered = CollisionHitPointExportSelector.ForCylindricalGeneratedHits(records);

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, record => Assert.Equal(CollisionRaySourceType.CylindricalGenerated, record.SourceType));
    }

    [Fact]
    public void Selector_PreservesDuplicates()
    {
        var duplicatePoint = new Vector3(3f, 3f, 3f);
        var records = new[]
        {
            new CollisionHitPointRecord("Scene A", duplicatePoint, CollisionRaySourceType.CylindricalGenerated),
            new CollisionHitPointRecord("Scene A", duplicatePoint, CollisionRaySourceType.CylindricalGenerated),
        };

        var filtered = CollisionHitPointExportSelector.ForCylindricalGeneratedHits(records);

        Assert.Equal(2, filtered.Count);
        Assert.Equal(filtered[0].HitPoint, filtered[1].HitPoint);
    }

    [Fact]
    public void CsvExport_WritesHeaderAndRowsWithInvariantCulture()
    {
        var exportService = new CollisionHitPointCsvExportService();
        var records = new[]
        {
            new CollisionHitPointRecord("Scene A", new Vector3(1.5f, 2.25f, -3.75f), CollisionRaySourceType.CylindricalGenerated),
            new CollisionHitPointRecord("Scene B", new Vector3(0f, -1f, 99.125f), CollisionRaySourceType.CylindricalGenerated),
        };

        var path = Path.Combine(Path.GetTempPath(), $"hit-export-{Guid.NewGuid():N}.csv");

        try
        {
            exportService.Export(path, records);
            var lines = File.ReadAllLines(path);

            Assert.Equal("SceneName,HitX,HitY,HitZ", lines[0]);
            Assert.Equal(
                string.Create(CultureInfo.InvariantCulture, $"Scene A,{records[0].HitPoint.X},{records[0].HitPoint.Y},{records[0].HitPoint.Z}"),
                lines[1]);
            Assert.Equal(
                string.Create(CultureInfo.InvariantCulture, $"Scene B,{records[1].HitPoint.X},{records[1].HitPoint.Y},{records[1].HitPoint.Z}"),
                lines[2]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
