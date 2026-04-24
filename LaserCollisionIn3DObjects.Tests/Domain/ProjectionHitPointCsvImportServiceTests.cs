using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class ProjectionHitPointCsvImportServiceTests
{
    [Fact]
    public void Import_ParsesValidRowsAndPreservesDuplicates()
    {
        var service = new ProjectionHitPointCsvImportService();
        var filePath = Path.Combine(Path.GetTempPath(), $"projection-import-{Guid.NewGuid():N}.csv");
        var lines = new[]
        {
            "SceneName,HitX,HitY,HitZ",
            "Scene A,1.5,2.5,3.5",
            "Scene A,1.5,2.5,3.5",
        };

        try
        {
            File.WriteAllLines(filePath, lines);
            var result = service.Import(filePath);

            Assert.Equal("Scene A", result.SceneName);
            Assert.Equal(2, result.HolePoints.Count);
            Assert.Equal(new Point3(1.5, 2.5, 3.5), result.HolePoints[0]);
            Assert.Equal(result.HolePoints[0], result.HolePoints[1]);
            Assert.Equal(0, result.SkippedRowCount);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void Import_SkipsInvalidRowsAndKeepsValidRows()
    {
        var service = new ProjectionHitPointCsvImportService();
        var filePath = Path.Combine(Path.GetTempPath(), $"projection-import-{Guid.NewGuid():N}.csv");
        var lines = new[]
        {
            "SceneName,HitX,HitY,HitZ",
            "Scene A,1,2,3",
            "Scene A,NaN,2,3",
            "Scene A,4,5",
            ",7,8,9",
            "Scene A,10,11,12",
        };

        try
        {
            File.WriteAllLines(filePath, lines);
            var result = service.Import(filePath);

            Assert.Equal("Scene A", result.SceneName);
            Assert.Equal(2, result.HolePoints.Count);
            Assert.Equal(3, result.SkippedRowCount);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void Import_WithQuotedSceneName_ParsesCsvEscaping()
    {
        var service = new ProjectionHitPointCsvImportService();
        var filePath = Path.Combine(Path.GetTempPath(), $"projection-import-{Guid.NewGuid():N}.csv");
        var lines = new[]
        {
            "SceneName,HitX,HitY,HitZ",
            "\"Scene, A\",1,2,3",
        };

        try
        {
            File.WriteAllLines(filePath, lines);
            var result = service.Import(filePath);

            Assert.Equal("Scene, A", result.SceneName);
            Assert.Single(result.HolePoints);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void Import_ThrowsForInvalidHeader()
    {
        var service = new ProjectionHitPointCsvImportService();
        var filePath = Path.Combine(Path.GetTempPath(), $"projection-import-{Guid.NewGuid():N}.csv");

        try
        {
            File.WriteAllLines(filePath, ["Wrong,Columns,Only"]);
            var ex = Assert.Throws<ArgumentException>(() => service.Import(filePath));
            Assert.Contains("SceneName,HitX,HitY,HitZ", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
