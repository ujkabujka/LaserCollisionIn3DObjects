using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LaserCollisionIn3DObjects.Domain.Imaging;

namespace LaserCollisionIn3DObjects.Wpf.Views;

public partial class AnnotationWorkspaceView : UserControl
{
    private const double ZoomStepFactor = 1.15;
    private const double MinManualZoom = 0.01;
    private const double MaxZoom = 20.0;
    private bool _isOriginalUserZoomActive;
    private bool _isWarpedUserZoomActive;

    public AnnotationWorkspaceView() => InitializeComponent();

    private void OnAnnotationViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        var isOriginalViewer = ReferenceEquals(sender, OriginalScrollViewer);
        var scrollViewer = isOriginalViewer ? OriginalScrollViewer : WarpedScrollViewer;
        var canvas = isOriginalViewer ? OriginalCanvas : WarpedCanvas;
        var scaleTransform = isOriginalViewer ? OriginalScaleTransform : WarpedScaleTransform;
        if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return;

        var factor = e.Delta > 0 ? ZoomStepFactor : 1.0 / ZoomStepFactor;
        var nextScale = Math.Clamp(scaleTransform.ScaleX * factor, MinManualZoom, MaxZoom);
        SetScaleAroundViewportPoint(scrollViewer, canvas, scaleTransform, nextScale, e.GetPosition(scrollViewer));
        if (isOriginalViewer) _isOriginalUserZoomActive = true;
        else _isWarpedUserZoomActive = true;
        e.Handled = true;
    }

    private void OnOriginalPreviewImageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCanvasSize(OriginalCanvas, OriginalPreviewImage);
        RequestOriginalFit(resetManualZoom: true);
    }

    private void OnWarpedPreviewImageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCanvasSize(WarpedCanvas, WarpedPreviewImage);
        RequestWarpedFit(resetManualZoom: true);
    }

    private void OnOriginalPreviewImageTargetUpdated(object sender, DataTransferEventArgs e) => RequestOriginalFit(resetManualZoom: true);

    private void OnWarpedPreviewImageTargetUpdated(object sender, DataTransferEventArgs e) => RequestWarpedFit(resetManualZoom: true);

    private void OnOriginalScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isOriginalUserZoomActive) RequestOriginalFit(resetManualZoom: false);
    }

    private void OnWarpedScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isWarpedUserZoomActive) RequestWarpedFit(resetManualZoom: false);
    }

    private void RequestOriginalFit(bool resetManualZoom)
    {
        if (resetManualZoom) _isOriginalUserZoomActive = false;
        if (!_isOriginalUserZoomActive) RequestFit(OriginalScrollViewer, OriginalCanvas, OriginalScaleTransform);
    }

    private void RequestWarpedFit(bool resetManualZoom)
    {
        if (resetManualZoom) _isWarpedUserZoomActive = false;
        if (!_isWarpedUserZoomActive) RequestFit(WarpedScrollViewer, WarpedCanvas, WarpedScaleTransform);
    }

    private void RequestFit(ScrollViewer scrollViewer, Canvas canvas, ScaleTransform scaleTransform)
        => Dispatcher.BeginInvoke(DispatcherPriority.Render, () => FitCanvasToScrollViewer(scrollViewer, canvas, scaleTransform));

    private static void UpdateCanvasSize(Canvas canvas, Image previewImage)
    {
        canvas.Width = previewImage.ActualWidth;
        canvas.Height = previewImage.ActualHeight;
    }

    private static void FitCanvasToScrollViewer(ScrollViewer scrollViewer, FrameworkElement canvas, ScaleTransform scaleTransform)
    {
        var fitScale = ViewerFitScaleCalculator.CalculateFitScale(canvas.ActualWidth, canvas.ActualHeight, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight);
        if (fitScale is null) return;

        scaleTransform.ScaleX = fitScale.Value;
        scaleTransform.ScaleY = fitScale.Value;
        CenterCanvas(scrollViewer, canvas, fitScale.Value);
        scrollViewer.ScrollToHorizontalOffset(0);
        scrollViewer.ScrollToVerticalOffset(0);
    }

    private static void SetScaleAroundViewportPoint(ScrollViewer scrollViewer, FrameworkElement canvas, ScaleTransform scaleTransform, double newScale, Point viewportPoint)
    {
        var oldScale = scaleTransform.ScaleX;
        if (oldScale <= 0 || Math.Abs(newScale - oldScale) < 0.0001) return;
        var contentX = (scrollViewer.HorizontalOffset + viewportPoint.X) / oldScale;
        var contentY = (scrollViewer.VerticalOffset + viewportPoint.Y) / oldScale;
        scaleTransform.ScaleX = newScale;
        scaleTransform.ScaleY = newScale;
        CenterCanvas(scrollViewer, canvas, newScale);
        scrollViewer.ScrollToHorizontalOffset(Math.Max(0, contentX * newScale - viewportPoint.X));
        scrollViewer.ScrollToVerticalOffset(Math.Max(0, contentY * newScale - viewportPoint.Y));
    }

    private static void CenterCanvas(ScrollViewer scrollViewer, FrameworkElement canvas, double scale)
    {
        var horizontalMargin = Math.Max(0, (scrollViewer.ViewportWidth - canvas.ActualWidth * scale) / 2.0);
        var verticalMargin = Math.Max(0, (scrollViewer.ViewportHeight - canvas.ActualHeight * scale) / 2.0);
        canvas.Margin = new Thickness(horizontalMargin, verticalMargin, 0, 0);
    }
}
