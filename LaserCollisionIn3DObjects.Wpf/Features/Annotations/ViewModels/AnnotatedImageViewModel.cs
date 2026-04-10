using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

public sealed class AnnotatedImageViewModel : ObservableObject
{
    private double? _panelWidthMm;
    private double? _panelHeightMm;

    public required AnnotatedImageRecord Record { get; init; }

    public string DisplayName => Record.IsImageMissing ? $"{Record.FileName} (missing)" : Record.FileName;

    public string FileName => Record.FileName;

    public bool HasPanel => Record.Panel is not null;

    public int HoleCount => Record.Holes.Count;

    public BitmapSource? OriginalImage { get; set; }

    public BitmapSource? OriginalOverlay { get; set; }

    public BitmapSource? WarpedImage { get; set; }

    public BitmapSource? WarpedOverlay { get; set; }

    public ObservableCollection<HoleViewModel> Holes { get; } = new();
    public ObservableCollection<Point> WarpedHoleCentersMm { get; } = new();

    public string PanelCornersText { get; set; } = "N/A";

    public string DiagnosticsText => string.Join(" | ", Record.Diagnostics);

    public double? PanelWidthMm
    {
        get => _panelWidthMm;
        set
        {
            if (SetProperty(ref _panelWidthMm, value))
            {
                Record.Calibration.PhysicalWidthMm = value;
            }
        }
    }

    public double? PanelHeightMm
    {
        get => _panelHeightMm;
        set
        {
            if (SetProperty(ref _panelHeightMm, value))
            {
                Record.Calibration.PhysicalHeightMm = value;
            }
        }
    }
}
