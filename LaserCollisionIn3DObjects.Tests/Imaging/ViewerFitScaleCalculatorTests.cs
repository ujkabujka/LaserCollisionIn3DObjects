using LaserCollisionIn3DObjects.Domain.Imaging;

namespace LaserCollisionIn3DObjects.Tests.Imaging;

public sealed class ViewerFitScaleCalculatorTests
{
    [Theory]
    [InlineData(1920, 1080, 960, 540, 0.5)]
    [InlineData(6000, 8000, 600, 600, 0.075)]
    [InlineData(10000, 1000, 1000, 600, 0.1)]
    [InlineData(1000, 10000, 600, 1000, 0.1)]
    public void CalculateFitScale_FitsEntireImage(double width, double height, double viewportWidth, double viewportHeight, double expected)
        => Assert.Equal(expected, ViewerFitScaleCalculator.CalculateFitScale(width, height, viewportWidth, viewportHeight));

    [Fact]
    public void CalculateFitScale_DoesNotClampVeryLargeImages() => Assert.Equal(1d / 60d, ViewerFitScaleCalculator.CalculateFitScale(20000, 30000, 500, 500));

    [Theory]
    [InlineData(0, 10, 100, 100)]
    [InlineData(-1, 10, 100, 100)]
    [InlineData(10, 0, 100, 100)]
    public void CalculateFitScale_InvalidDimensions_ReturnsNull(double width, double height, double viewportWidth, double viewportHeight)
        => Assert.Null(ViewerFitScaleCalculator.CalculateFitScale(width, height, viewportWidth, viewportHeight));
}
