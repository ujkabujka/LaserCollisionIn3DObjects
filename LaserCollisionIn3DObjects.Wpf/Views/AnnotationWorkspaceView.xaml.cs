using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LaserCollisionIn3DObjects.Wpf.Views;

public partial class AnnotationWorkspaceView : UserControl
{
    private const double ZoomStepFactor = 1.15;
    private const double MinZoom = 0.05;
    private const double MaxZoom = 20.0;
    private bool _isOriginalUserZoomActive;
    private bool _isWarpedUserZoomActive;

    public AnnotationWorkspaceView()
    {
        InitializeComponent();
    }

    private void OnAnnotationViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        var isOriginalViewer = ReferenceEquals(sender, OriginalScrollViewer);
        var scrollViewer = isOriginalViewer ? OriginalScrollViewer : WarpedScrollViewer;
        var canvas = isOriginalViewer ? OriginalCanvas : WarpedCanvas;
        var scaleTransform = isOriginalViewer ? OriginalScaleTransform : WarpedScaleTransform;

        if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0)
        {
            return;
        }

        var factor = e.Delta > 0 ? ZoomStepFactor : 1.0 / ZoomStepFactor;
        var nextScale = Math.Clamp(scaleTransform.ScaleX * factor, MinZoom, MaxZoom);
        SetScaleAroundViewportPoint(scrollViewer, canvas, scaleTransform, nextScale, e.GetPosition(scrollViewer));
        if (isOriginalViewer)
        {
            _isOriginalUserZoomActive = true;
        }
        else
        {
            _isWarpedUserZoomActive = true;
        }

        e.Handled = true;
    }

    private void OnOriginalBaseImageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        OriginalCanvas.Width = OriginalBaseImage.ActualWidth;
        OriginalCanvas.Height = OriginalBaseImage.ActualHeight;
        _isOriginalUserZoomActive = false;
        FitCanvasToScrollViewer(OriginalScrollViewer, OriginalCanvas, OriginalScaleTransform);
    }

    private void OnWarpedBaseImageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        WarpedCanvas.Width = WarpedBaseImage.ActualWidth;
        WarpedCanvas.Height = WarpedBaseImage.ActualHeight;
        _isWarpedUserZoomActive = false;
        FitCanvasToScrollViewer(WarpedScrollViewer, WarpedCanvas, WarpedScaleTransform);
    }

    private void OnOriginalScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isOriginalUserZoomActive)
        {
            FitCanvasToScrollViewer(OriginalScrollViewer, OriginalCanvas, OriginalScaleTransform);
        }
    }

    private void OnWarpedScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isWarpedUserZoomActive)
        {
            FitCanvasToScrollViewer(WarpedScrollViewer, WarpedCanvas, WarpedScaleTransform);
        }
    }

    private static void FitCanvasToScrollViewer(ScrollViewer scrollViewer, FrameworkElement canvas, ScaleTransform scaleTransform)
    {
        if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0 || scrollViewer.ViewportWidth <= 0 || scrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var fitScale = Math.Min(scrollViewer.ViewportWidth / canvas.ActualWidth, scrollViewer.ViewportHeight / canvas.ActualHeight);
        fitScale = Math.Clamp(fitScale, MinZoom, MaxZoom);
        scaleTransform.ScaleX = fitScale;
        scaleTransform.ScaleY = fitScale;

        var scaledWidth = canvas.ActualWidth * fitScale;
        var scaledHeight = canvas.ActualHeight * fitScale;
        var horizontalMargin = Math.Max(0, (scrollViewer.ViewportWidth - scaledWidth) / 2.0);
        var verticalMargin = Math.Max(0, (scrollViewer.ViewportHeight - scaledHeight) / 2.0);
        canvas.Margin = new Thickness(horizontalMargin, verticalMargin, 0, 0);

        scrollViewer.ScrollToHorizontalOffset(0);
        scrollViewer.ScrollToVerticalOffset(0);
    }

    private static void SetScaleAroundViewportPoint(
        ScrollViewer scrollViewer,
        FrameworkElement canvas,
        ScaleTransform scaleTransform,
        double newScale,
        Point viewportPoint)
    {
        var oldScale = scaleTransform.ScaleX;
        if (oldScale <= 0 || Math.Abs(newScale - oldScale) < 0.0001)
        {
            return;
        }

        var contentX = (scrollViewer.HorizontalOffset + viewportPoint.X) / oldScale;
        var contentY = (scrollViewer.VerticalOffset + viewportPoint.Y) / oldScale;

        scaleTransform.ScaleX = newScale;
        scaleTransform.ScaleY = newScale;

        var scaledWidth = canvas.ActualWidth * newScale;
        var scaledHeight = canvas.ActualHeight * newScale;
        var horizontalMargin = Math.Max(0, (scrollViewer.ViewportWidth - scaledWidth) / 2.0);
        var verticalMargin = Math.Max(0, (scrollViewer.ViewportHeight - scaledHeight) / 2.0);
        canvas.Margin = new Thickness(horizontalMargin, verticalMargin, 0, 0);

        scrollViewer.ScrollToHorizontalOffset(Math.Max(0, contentX * newScale - viewportPoint.X));
        scrollViewer.ScrollToVerticalOffset(Math.Max(0, contentY * newScale - viewportPoint.Y));
    }
}
