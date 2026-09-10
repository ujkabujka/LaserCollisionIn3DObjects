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
    private double? _panelThicknessMm;
    private BitmapSource? _originalImage;
    private BitmapSource? _originalOverlay;
    private BitmapSource? _warpedImage;
    private BitmapSource? _warpedOverlay;

    public AnnotatedImageViewModel()
    {
        LeftTopCorner = new CornerMeasurementViewModel(CornerType.LeftTop);
        RightTopCorner = new CornerMeasurementViewModel(CornerType.RightTop);
        RightBottomCorner = new CornerMeasurementViewModel(CornerType.RightBottom);
        LeftBottomCorner = new CornerMeasurementViewModel(CornerType.LeftBottom);

        CornerMeasurements.Add(LeftTopCorner);
        CornerMeasurements.Add(RightTopCorner);
        CornerMeasurements.Add(RightBottomCorner);
        CornerMeasurements.Add(LeftBottomCorner);
    }

    public required AnnotatedImageRecord Record { get; init; }

    public string DisplayName => Record.IsImageMissing ? $"{Record.FileName} (missing)" : Record.FileName;

    public string FileName => Record.FileName;

    public bool HasPanel => Record.Panel is not null;

    public int HoleCount => Record.Holes.Count();
    public int NaturalCount => Record.NaturalPoints.Count();

    public BitmapSource? OriginalImage
    {
        get => _originalImage;
        set => SetProperty(ref _originalImage, value);
    }

    public BitmapSource? OriginalOverlay
    {
        get => _originalOverlay;
        set => SetProperty(ref _originalOverlay, value);
    }

    public BitmapSource? WarpedImage
    {
        get => _warpedImage;
        set => SetProperty(ref _warpedImage, value);
    }

    public BitmapSource? WarpedOverlay
    {
        get => _warpedOverlay;
        set => SetProperty(ref _warpedOverlay, value);
    }

    public ObservableCollection<AnnotatedPointViewModel> AnnotationPoints { get; } = new();
    public ObservableCollection<Point> WarpedHoleCentersMm { get; } = new();
    public ObservableCollection<Point> WarpedNaturalCentersMm { get; } = new();

    public ObservableCollection<CornerMeasurementViewModel> CornerMeasurements { get; } = new();

    public CornerMeasurementViewModel LeftTopCorner { get; }

    public CornerMeasurementViewModel RightTopCorner { get; }

    public CornerMeasurementViewModel RightBottomCorner { get; }

    public CornerMeasurementViewModel LeftBottomCorner { get; }

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

    public double? PanelThicknessMm
    {
        get => _panelThicknessMm;
        set => SetProperty(ref _panelThicknessMm, value);
    }
}
