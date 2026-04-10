using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

public sealed class AnnotatedImageViewModel
{
    public required AnnotatedImageRecord Record { get; init; }

    public string DisplayName => Record.IsImageMissing ? $"{Record.FileName} (missing)" : Record.FileName;

    public BitmapSource? OriginalImage { get; set; }

    public BitmapSource? OriginalOverlay { get; set; }

    public BitmapSource? WarpedImage { get; set; }

    public BitmapSource? WarpedOverlay { get; set; }

    public ObservableCollection<HoleViewModel> Holes { get; } = new();

    public string PanelCornersText { get; set; } = "N/A";

    public string DiagnosticsText => string.Join(" | ", Record.Diagnostics);
}
