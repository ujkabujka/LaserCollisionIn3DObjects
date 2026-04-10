using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using System.Windows.Media.Imaging;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

public sealed class AnnotationWorkspaceViewModel : ObservableObject
{
    private readonly AnnotationWorkspaceService _workspaceService = new();
    private readonly Dictionary<AnnotatedImageViewModel, RectificationResult?> _rectificationByImage = new();
    private string _selectedFolderPath = "No folder selected.";
    private string _statusMessage = "Select an annotation folder to begin.";
    private AnnotatedImageViewModel? _selectedImage;
    private double _globalPanelWidthMm = 1000;
    private double _globalPanelHeightMm = 1000;

    public AnnotationWorkspaceViewModel()
    {
        SelectFolderCommand = new RelayCommand(SelectFolder);
        SelectPreviousImageCommand = new RelayCommand(SelectPreviousImage, () => SelectedImageIndex > 0);
        SelectNextImageCommand = new RelayCommand(SelectNextImage, () => SelectedImageIndex >= 0 && SelectedImageIndex < Images.Count - 1);
        ApplyGlobalPanelDimensionsCommand = new RelayCommand(ApplyGlobalPanelDimensions, () => Images.Count > 0);
    }

    public ICommand SelectFolderCommand { get; }

    public ICommand SelectPreviousImageCommand { get; }

    public ICommand SelectNextImageCommand { get; }

    public ICommand ApplyGlobalPanelDimensionsCommand { get; }

    public ObservableCollection<AnnotatedImageViewModel> Images { get; } = new();

    public string SelectedFolderPath
    {
        get => _selectedFolderPath;
        set => SetProperty(ref _selectedFolderPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AnnotatedImageViewModel? SelectedImage
    {
        get => _selectedImage;
        set
        {
            if (!SetProperty(ref _selectedImage, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(SelectedImageIndex));
            RaisePropertyChanged(nameof(SelectedImageSummary));
            RaiseCanExecuteChanges();
            if (value is not null)
            {
                ProcessSelectedImage(value);
            }
        }
    }

    public int SelectedImageIndex => SelectedImage is null ? -1 : Images.IndexOf(SelectedImage);

    public string SelectedImageSummary => SelectedImage is null
        ? "No image selected."
        : $"File: {SelectedImage.FileName} | Panel: {(SelectedImage.HasPanel ? "Yes" : "No")} | Holes: {SelectedImage.HoleCount}";

    public IReadOnlyDictionary<string, IReadOnlyList<Point>> WarpedHoleCentersMmByImage
        => Images.ToDictionary(
            static image => image.FileName,
            static image => (IReadOnlyList<Point>)image.WarpedHoleCentersMm.ToList());

    public double GlobalPanelWidthMm
    {
        get => _globalPanelWidthMm;
        set => SetProperty(ref _globalPanelWidthMm, value);
    }

    public double GlobalPanelHeightMm
    {
        get => _globalPanelHeightMm;
        set => SetProperty(ref _globalPanelHeightMm, value);
    }

    private void SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder containing VIA JSON and corresponding images.",
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        SelectedFolderPath = dialog.FolderName;
        LoadProject(dialog.FolderName);
    }

    private void SelectPreviousImage()
    {
        if (SelectedImageIndex > 0)
        {
            SelectedImage = Images[SelectedImageIndex - 1];
        }
    }

    private void SelectNextImage()
    {
        if (SelectedImageIndex >= 0 && SelectedImageIndex < Images.Count - 1)
        {
            SelectedImage = Images[SelectedImageIndex + 1];
        }
    }

    private void LoadProject(string folderPath)
    {
        Images.Clear();
        _rectificationByImage.Clear();

        try
        {
            var project = _workspaceService.LoadProject(folderPath);
            foreach (var record in project.Images)
            {
                if (record.Panel is not null)
                {
                    try
                    {
                        _workspaceService.FitPanel(record);
                    }
                    catch (Exception ex)
                    {
                        record.Diagnostics.Add($"Panel fitting failed: {ex.Message}");
                    }
                }

                var viewModel = new AnnotatedImageViewModel
                {
                    Record = record,
                    PanelWidthMm = record.Calibration.PhysicalWidthMm,
                    PanelHeightMm = record.Calibration.PhysicalHeightMm,
                };
                viewModel.PropertyChanged += OnImageCalibrationChanged;
                Images.Add(viewModel);
            }

            StatusMessage = $"Loaded {Images.Count} image annotations from {Path.GetFileName(project.JsonFilePath)}.";
            SelectedImage = Images.FirstOrDefault();
            RaiseCanExecuteChanges();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void ProcessSelectedImage(AnnotatedImageViewModel selected)
    {
        selected.Holes.Clear();
        if (selected.Record.IsImageMissing)
        {
            StatusMessage = $"Image missing for '{selected.Record.FileName}'.";
            return;
        }

        try
        {
            selected.OriginalImage = _workspaceService.LoadImage(selected.Record.ImagePath!);
            selected.OriginalOverlay = _workspaceService.CreateOriginalOverlay(selected.Record, selected.OriginalImage);
            SaveBitmapSourceAsPng(selected.OriginalOverlay, @"C:\Users\ugurcan.karaca\Desktop\Example Images\debug1.png");

            var rectified = _workspaceService.CreateRectification(selected.Record, selected.OriginalImage);
            if (rectified is not null)
            {
                selected.WarpedImage = rectified.WarpedImage;
                selected.WarpedOverlay = _workspaceService.CreateWarpedOverlay(rectified, rectified.WarpedImage);
            }
            else
            {
                selected.WarpedImage = null;
                selected.WarpedOverlay = null;
            }

            _rectificationByImage[selected] = rectified;
            RebuildHoleRows(selected);

            selected.PanelCornersText = selected.Record.Panel is null
                ? "No panel"
                : string.Join("; ", selected.Record.Panel.FittedQuadrilateralCorners.Select(static p => $"({p.X:F1}, {p.Y:F1})"));

            StatusMessage = string.IsNullOrWhiteSpace(selected.DiagnosticsText)
                ? $"Loaded {selected.Record.FileName}: {selected.Record.Holes.Count} holes."
                : $"Loaded with diagnostics: {selected.DiagnosticsText}";
            RaisePropertyChanged(nameof(SelectedImage));
            RaisePropertyChanged(nameof(SelectedImageSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to process selected image: {ex.Message}";
        }
    }

    private void RaiseCanExecuteChanges()
    {
        (SelectPreviousImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SelectNextImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyGlobalPanelDimensionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ApplyGlobalPanelDimensions()
    {
        if (GlobalPanelWidthMm <= 0 || GlobalPanelHeightMm <= 0)
        {
            StatusMessage = "Global panel dimensions must be greater than zero.";
            return;
        }

        foreach (var image in Images)
        {
            image.PanelWidthMm = GlobalPanelWidthMm;
            image.PanelHeightMm = GlobalPanelHeightMm;
            RebuildHoleRows(image);
        }

        StatusMessage = $"Applied {GlobalPanelWidthMm:F2}mm x {GlobalPanelHeightMm:F2}mm to {Images.Count} images.";
    }

    private void OnImageCalibrationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not AnnotatedImageViewModel image
            || (e.PropertyName != nameof(AnnotatedImageViewModel.PanelWidthMm) && e.PropertyName != nameof(AnnotatedImageViewModel.PanelHeightMm)))
        {
            return;
        }

        RebuildHoleRows(image);
    }

    private void RebuildHoleRows(AnnotatedImageViewModel image)
    {
        image.Holes.Clear();
        image.WarpedHoleCentersMm.Clear();
        _rectificationByImage.TryGetValue(image, out var rectification);
        var canConvertToMm = rectification is not null
            && image.Record.Calibration.IsConfigured
            && rectification.DestinationSizePixels.Width > 0
            && rectification.DestinationSizePixels.Height > 0;
        var mmScaleX = canConvertToMm ? image.Record.Calibration.PhysicalWidthMm!.Value / rectification!.DestinationSizePixels.Width : 0d;
        var mmScaleY = canConvertToMm ? image.Record.Calibration.PhysicalHeightMm!.Value / rectification!.DestinationSizePixels.Height : 0d;

        foreach (var row in AnnotationWorkspaceService.BuildHoleRows(image.Record, rectification, image.Record.Calibration))
        {
            image.Holes.Add(row);
        }

        if (canConvertToMm)
        {
            foreach (var point in rectification!.TransformedHoleCenters)
            {
                image.WarpedHoleCentersMm.Add(new Point(point.X * mmScaleX, point.Y * mmScaleY));
            }
        }

        RaisePropertyChanged(nameof(WarpedHoleCentersMmByImage));
    }

    public static void SaveBitmapSourceAsPng(BitmapSource bitmap, string path)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }
}
