using LaserCollisionIn3DObjects.Domain.Import;

namespace LaserCollisionIn3DObjects.Tests.Import;

public sealed class PanelMeasurementsCsvImportServiceTests
{
    private readonly PanelMeasurementsCsvImportService _service = new();
    private const string Data = "100,200,10,1,2,3,4,5,6,7,8,9,10,11,12";

    [Fact]
    public void Parse_HeaderlessCsv_ParsesFixedCornerOrder() 
    {
        var row = Assert.Single(_service.Parse(new StringReader(Data)));
        Assert.Equal(100, row.WidthMm); Assert.Equal(200, row.HeightMm); Assert.Equal(10, row.ThicknessMm);
        Assert.Equal(1, row.LeftTop.DistanceMeters); Assert.Equal(4, row.RightTop.DistanceMeters);
        Assert.Equal(7, row.RightBottom.DistanceMeters); Assert.Equal(10, row.LeftBottom.DistanceMeters);
    }

    [Fact]
    public void Parse_HeaderCsvAndEmptyLines_ParsesData() 
    {
        const string header = "\uFEFFWidth,Height,Thickness,LT_R,LT_Azimuth,LT_Elevation,RT_R,RT_Azimuth,RT_Elevation,RB_R,RB_Azimuth,RB_Elevation,LB_R,LB_Azimuth,LB_Elevation";
        var row = Assert.Single(_service.Parse(new StringReader($"\n{header}\n\n{Data}\n")));
        Assert.Equal(12, row.LeftBottom.ElevationDeg);
    }

    [Theory]
    [InlineData("1,2,3,4,5,6,7,8,9,10,11,12,13,14")]
    [InlineData("1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16")]
    public void Parse_IncorrectColumnCount_Throws(string csv) => Assert.Throws<FormatException>(() => _service.Parse(new StringReader(csv)));

    [Theory]
    [InlineData("nope")]
    [InlineData("0")]
    public void Parse_InvalidOrNonPositiveField_Throws(string invalid) => Assert.Throws<FormatException>(() => _service.Parse(new StringReader(Data.Replace("100", invalid))));

    [Fact]
    public void Parse_NonPositiveDistance_Throws() => Assert.Throws<FormatException>(() => _service.Parse(new StringReader(Data.Replace("1,2,3", "0,2,3"))));

    [Fact]
    public void NaturalOrdering_OrdersNumericFilenameSegments() 
    {
        var names = new[] { "Panel10.jpg", "Panel2.jpg", "Panel1.jpg" };
        Assert.Equal(new[] { "Panel1.jpg", "Panel2.jpg", "Panel10.jpg" }, names.OrderBy(static name => name, NaturalFileNameComparer.Instance));
    }

    [Theory]
    [InlineData("panel")]
    [InlineData(" Plane ")]
    [InlineData("3")]
    public void AnnotationTypeClassifier_RecognizesPanelAliases(string type) => Assert.True(AnnotationTypeClassifier.IsPanel(type));

    [Theory]
    [InlineData("hole")]
    [InlineData("natural")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("99")]
    public void AnnotationTypeClassifier_TreatsAllOtherTypesAsCollisionPoints(string type) => Assert.False(AnnotationTypeClassifier.IsPanel(type));

}
