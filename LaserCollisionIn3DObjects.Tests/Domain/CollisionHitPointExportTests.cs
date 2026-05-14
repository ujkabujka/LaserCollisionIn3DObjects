using System.Globalization;
using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Export;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class CollisionHitPointExportTests
{
    [Fact]
    public void CsvExport_IncludesSourceTypeHeader()
    {
        var exportService = new CollisionHitPointCsvExportService();
        var records = new[]
        {
            new CollisionHitPointRecord("Scene A", new Vector3(1, 2, 3), CollisionRaySourceType.CylindricalGenerated),
        };
        var path = Path.Combine(Path.GetTempPath(), $"hit-export-{Guid.NewGuid():N}.csv");

        try
        {
            exportService.Export(path, records);
            var lines = File.ReadAllLines(path);
            Assert.Equal("SceneName,SourceType,HitX,HitY,HitZ", lines[0]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CsvExport_WritesAllSourceTypes()
    {
        var exportService = new CollisionHitPointCsvExportService();
        var records = new[]
        {
            new CollisionHitPointRecord("Scene A", new Vector3(1f, 2f, 3f), CollisionRaySourceType.CylindricalGenerated),
            new CollisionHitPointRecord("Scene A", new Vector3(2f, 3f, 4f), CollisionRaySourceType.ConicalFrustumGenerated),
            new CollisionHitPointRecord("Scene A", new Vector3(3f, 4f, 5f), CollisionRaySourceType.CircularOgiveGenerated),
            new CollisionHitPointRecord("Scene A", new Vector3(4f, 5f, 6f), CollisionRaySourceType.HybridAxisymmetricGenerated),
            new CollisionHitPointRecord("Scene A", new Vector3(5f, 6f, 7f), CollisionRaySourceType.ProjectionResult),
            new CollisionHitPointRecord("Scene A", new Vector3(6f, 7f, 8f), CollisionRaySourceType.Manual),
        };
        var path = Path.Combine(Path.GetTempPath(), $"hit-export-{Guid.NewGuid():N}.csv");

        try
        {
            exportService.Export(path, records);
            var csv = File.ReadAllText(path);
            foreach (var sourceType in Enum.GetValues<CollisionRaySourceType>())
            {
                Assert.Contains(sourceType.ToString(), csv, StringComparison.Ordinal);
            }
            Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"{records[0].HitPoint.X}"), csv, StringComparison.Ordinal);
            Assert.Equal(records.Length + 1, File.ReadAllLines(path).Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
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

            Assert.Equal("SceneName,SourceType,HitX,HitY,HitZ", lines[0]);
            Assert.Equal(
                string.Create(CultureInfo.InvariantCulture, $"Scene A,CylindricalGenerated,{records[0].HitPoint.X},{records[0].HitPoint.Y},{records[0].HitPoint.Z}"),
                lines[1]);
            Assert.Equal(
                string.Create(CultureInfo.InvariantCulture, $"Scene B,CylindricalGenerated,{records[1].HitPoint.X},{records[1].HitPoint.Y},{records[1].HitPoint.Z}"),
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
