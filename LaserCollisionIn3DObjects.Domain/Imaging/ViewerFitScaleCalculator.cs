namespace LaserCollisionIn3DObjects.Domain.Imaging;

public static class ViewerFitScaleCalculator
{
    public static double? CalculateFitScale(double contentWidth, double contentHeight, double viewportWidth, double viewportHeight)
    {
        if (!double.IsFinite(contentWidth) || !double.IsFinite(contentHeight)
            || !double.IsFinite(viewportWidth) || !double.IsFinite(viewportHeight)
            || contentWidth <= 0 || contentHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return null;
        }

        var scale = Math.Min(viewportWidth / contentWidth, viewportHeight / contentHeight);
        return double.IsFinite(scale) && scale > 0 ? scale : null;
    }
}
