using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using LaserCollisionIn3DObjects.Domain.Persistence;
using LaserCollisionIn3DObjects.Domain.Import;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;
using System.Windows.Media.Media3D;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Generation;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

public sealed class AnnotationWorkspaceViewModel : ObservableObject
{
    private readonly SceneCollectionService? _sceneCollectionService;
    private readonly AnnotationWorkspaceService _workspaceService = new();
    private readonly PanelMeasurementsCsvImportService _panelMeasurementsImporter = new();
    private readonly Dictionary<AnnotatedImageViewModel, RectificationResult?> _rectificationByImage = new();
    private string _selectedFolderPath = "No folder selected.";
    private string _statusMessage = "Select an annotation folder to begin.";
    private AnnotatedImageViewModel? _selectedImage;
    private double _globalPanelWidthMm = 1000;
    private double _globalPanelHeightMm = 1000;
    private double _globalPanelThicknessMm = 10;
    private bool _isFolderResolved;
    private string? _missingFolderPath;
    private AnnotationWorkspaceState? _pendingWorkspaceState;
    private PrismGenerationMethodology _selectedPrismGenerationMethodology;
    private bool _isBusy;
    private string _busyMessage = string.Empty;
    private double _busyProgressPercent;
    private bool _isBusyIndeterminate = true;

    public AnnotationWorkspaceViewModel(SceneCollectionService? sceneCollectionService = null)
    {
        _sceneCollectionService = sceneCollectionService;
        SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync, () => !IsBusy);
        ImportPanelMeasurementsCsvCommand = new AsyncRelayCommand(ImportPanelMeasurementsCsvAsync, () => !IsBusy && Images.Count > 0);
        SelectPreviousImageCommand = new RelayCommand(SelectPreviousImage, () => !IsBusy && SelectedImageIndex > 0);
        SelectNextImageCommand = new RelayCommand(SelectNextImage, () => !IsBusy && SelectedImageIndex >= 0 && SelectedImageIndex < Images.Count - 1);
        ApplyGlobalPanelDimensionsCommand = new RelayCommand(ApplyGlobalPanelDimensions, () => !IsBusy && Images.Count > 0);
        GenerateSceneCommand = new AsyncRelayCommand(GenerateSceneAsync, () => !IsBusy && Images.Count > 0);
        RelinkMissingFolderCommand = new AsyncRelayCommand(RestoreMissingFolderAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(MissingFolderPath));
    }

    public ICommand SelectFolderCommand { get; }

    public ICommand ImportPanelMeasurementsCsvCommand { get; }

    public ICommand SelectPreviousImageCommand { get; }

    public ICommand SelectNextImageCommand { get; }

    public ICommand ApplyGlobalPanelDimensionsCommand { get; }

    public ICommand GenerateSceneCommand { get; }
    public ICommand RelinkMissingFolderCommand { get; }

    public ObservableCollection<AnnotatedImageViewModel> Images { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) RaiseCanExecuteChanges();
        }
    }

    public string BusyMessage { get => _busyMessage; private set => SetProperty(ref _busyMessage, value); }
    public double BusyProgressPercent { get => _busyProgressPercent; private set => SetProperty(ref _busyProgressPercent, value); }
    public bool IsBusyIndeterminate { get => _isBusyIndeterminate; private set => SetProperty(ref _isBusyIndeterminate, value); }

    public CornerMeasurementMode[] CornerMeasurementModes { get; } = Enum.GetValues<CornerMeasurementMode>();
    public PrismGenerationMethodology[] PrismGenerationMethodologies { get; } = Enum.GetValues<PrismGenerationMethodology>();
    public PrismGenerationMethodology SelectedPrismGenerationMethodology
    {
        get => _selectedPrismGenerationMethodology;
        set => SetProperty(ref _selectedPrismGenerationMethodology, value);
    }

    // TODO Phase 2: generate collision scenes from annotation data and push through this shared scene collection.
    public SceneCollectionService? SceneCollectionService => _sceneCollectionService;

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
                _ = ProcessSelectedImageAsync(value, updateStatus: true);
            }
        }
    }

    public int SelectedImageIndex => SelectedImage is null ? -1 : Images.IndexOf(SelectedImage);

    public string SelectedImageSummary => SelectedImage is null
        ? "No image selected."
        : $"File: {SelectedImage.FileName} | Panel: {(SelectedImage.HasPanel ? "Yes" : "No")} | Holes: {SelectedImage.HoleCount} | Natural: {SelectedImage.NaturalCount}";

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

    public double GlobalPanelThicknessMm
    {
        get => _globalPanelThicknessMm;
        set => SetProperty(ref _globalPanelThicknessMm, value);
    }

    public bool IsFolderResolved
    {
        get => _isFolderResolved;
        private set => SetProperty(ref _isFolderResolved, value);
    }

    public string? MissingFolderPath
    {
        get => _missingFolderPath;
        private set
        {
            if (SetProperty(ref _missingFolderPath, value))
            {
                (RelinkMissingFolderCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task SelectFolderAsync()
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

        await LoadProjectAsync(dialog.FolderName);
    }

    private async Task ImportPanelMeasurementsCsvAsync()
    {
        if (Images.Count == 0)
        {
            StatusMessage = "Load annotation images before importing panel measurements.";
            return;
        }

        var dialog = new OpenFileDialog { Title = "Import Panel Measurements CSV", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        BeginBusy("Importing panel measurements...");
        try
        {
            var csv = await File.ReadAllTextAsync(dialog.FileName);
            using var reader = new StringReader(csv);
            var rows = _panelMeasurementsImporter.Parse(reader);
            if (rows.Count != Images.Count)
            {
                StatusMessage = $"Panel measurements CSV has {rows.Count} data rows but {Images.Count} annotation images are loaded. No values were applied.";
                return;
            }

            var orderedImages = Images.OrderBy(static image => image.FileName, AnnotationImageFileNameComparer.Instance).ToList();
            for (var i = 0; i < orderedImages.Count; i++) ApplyPanelMeasurements(orderedImages[i], rows[i]);

            var failedImages = new List<string>();
            for (var i = 0; i < orderedImages.Count; i++)
            {
                ReportBusyProgress("Rectifying panels", i + 1, orderedImages.Count);
                if (!await EnsureImageRectificationAsync(orderedImages[i], updatePreview: orderedImages[i] == SelectedImage))
                    failedImages.Add(orderedImages[i].FileName);
            }

            RaiseCanExecuteChanges();
            StatusMessage = failedImages.Count == 0
                ? $"Imported panel measurements for {rows.Count} panel record(s)."
                : $"Imported panel measurements for {rows.Count} panel record(s), but rectification failed for: {string.Join(", ", failedImages)}.";
        }
        catch (FormatException ex) { StatusMessage = $"Panel measurements CSV is invalid: {ex.Message}"; }
        catch (Exception ex)
        {
            StatusMessage = $"Panel measurements CSV import failed: {ex.Message}";
        }
        finally { EndBusy(); }
    }

    public bool ImportPanelMeasurements(TextReader reader)
    {
        IReadOnlyList<PanelMeasurementRecord> rows;
        try { rows = _panelMeasurementsImporter.Parse(reader); }
        catch (FormatException ex) { StatusMessage = $"Panel measurements CSV is invalid: {ex.Message}"; return false; }

        if (rows.Count != Images.Count)
        {
            StatusMessage = $"Panel measurements CSV has {rows.Count} data rows but {Images.Count} annotation images are loaded. No values were applied.";
            return false;
        }

        var orderedImages = Images.OrderBy(static image => image.FileName, AnnotationImageFileNameComparer.Instance).ToList();
        for (var i = 0; i < orderedImages.Count; i++) ApplyPanelMeasurements(orderedImages[i], rows[i]);
        var failedImages = orderedImages.Where(image => !EnsureImageRectification(image, updatePreview: image == SelectedImage)).Select(static image => image.FileName).ToList();
        RaiseCanExecuteChanges();
        StatusMessage = failedImages.Count == 0
            ? $"Imported panel measurements for {rows.Count} panel record(s)."
            : $"Imported panel measurements for {rows.Count} panel record(s), but rectification failed for: {string.Join(", ", failedImages)}.";
        return true;
    }

    private static void ApplyPanelMeasurements(AnnotatedImageViewModel image, PanelMeasurementRecord row)
    {
        image.PanelWidthMm = row.WidthMm;
        image.PanelHeightMm = row.HeightMm;
        image.PanelThicknessMm = row.ThicknessMm;
        ApplyCorner(image.LeftTopCorner, row.LeftTop);
        ApplyCorner(image.RightTopCorner, row.RightTop);
        ApplyCorner(image.RightBottomCorner, row.RightBottom);
        ApplyCorner(image.LeftBottomCorner, row.LeftBottom);
    }

    private static void ApplyCorner(CornerMeasurementViewModel corner, PanelCornerMeasurement measurement)
    {
        corner.SelectedMode = CornerMeasurementMode.ManualMeasurement;
        if (measurement.DistanceMeters > 1000) { corner.ManualDistanceMeters = measurement.DistanceMeters / 1000; }
        else { corner.ManualDistanceMeters = measurement.DistanceMeters; }
        corner.ManualAzimuthDeg = measurement.AzimuthDeg;
        corner.ManualElevationDeg = measurement.ElevationDeg;
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
                    PanelThicknessMm = GlobalPanelThicknessMm,
                };
                viewModel.PropertyChanged += OnImageCalibrationChanged;
                Images.Add(viewModel);
            }

            StatusMessage = $"Loaded {Images.Count} image annotations from {Path.GetFileName(project.JsonFilePath)}.";
            SelectedImage = Images.FirstOrDefault();
            IsFolderResolved = true;
            MissingFolderPath = null;
            RaiseCanExecuteChanges();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsFolderResolved = false;
            MissingFolderPath = folderPath;
        }
    }

    private async Task LoadProjectAsync(string folderPath)
    {
        BeginBusy("Loading annotation file...");
        try
        {
            var project = await Task.Run(() => _workspaceService.LoadProject(folderPath));
            BusyMessage = "Checking annotations...";
            for (var i = 0; i < project.Images.Count; i++)
            {
                var record = project.Images[i];
                ReportBusyProgress("Fitting panels", i + 1, project.Images.Count);
                if (record.Panel is null) continue;
                try { await Task.Run(() => _workspaceService.FitPanel(record)); }
                catch (Exception ex) { record.Diagnostics.Add($"Panel fitting failed: {ex.Message}"); }
            }

            Images.Clear();
            _rectificationByImage.Clear();
            foreach (var record in project.Images)
            {
                var viewModel = new AnnotatedImageViewModel
                {
                    Record = record,
                    PanelWidthMm = record.Calibration.PhysicalWidthMm,
                    PanelHeightMm = record.Calibration.PhysicalHeightMm,
                    PanelThicknessMm = GlobalPanelThicknessMm,
                };
                viewModel.PropertyChanged += OnImageCalibrationChanged;
                Images.Add(viewModel);
            }

            SelectedFolderPath = folderPath;
            IsFolderResolved = true;
            MissingFolderPath = null;
            var firstImage = Images.FirstOrDefault();
            if (firstImage is not null)
            {
                BusyMessage = "Preparing first image...";
                SetSelectedImageWithoutProcessing(firstImage);
                await ProcessSelectedImageAsync(firstImage, updateStatus: false);
            }

            var duplicateCount = project.Images.Sum(static image => image.RemovedDuplicateAnnotationCount);
            var affectedImages = project.Images.Count(static image => image.RemovedDuplicateAnnotationCount > 0);
            StatusMessage = $"Loaded {Images.Count} image annotations from {Path.GetFileName(project.JsonFilePath)}."
                + (duplicateCount > 0 ? $" Removed {duplicateCount} duplicate annotations from {affectedImages} images." : string.Empty);
            RaiseCanExecuteChanges();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsFolderResolved = false;
            MissingFolderPath = folderPath;
        }
        finally { EndBusy(); }
    }

    private void SetSelectedImageWithoutProcessing(AnnotatedImageViewModel? value)
    {
        if (!SetProperty(ref _selectedImage, value)) return;
        RaisePropertyChanged(nameof(SelectedImageIndex));
        RaisePropertyChanged(nameof(SelectedImageSummary));
        RaiseCanExecuteChanges();
    }

    private async Task ProcessSelectedImageAsync(AnnotatedImageViewModel selected, bool updateStatus)
    {
        var rectified = await EnsureImageRectificationAsync(selected, updatePreview: true);
        selected.PanelCornersText = selected.Record.Panel is null
            ? "No panel"
            : string.Join("; ", selected.Record.Panel.FittedQuadrilateralCorners.Select(static p => $"({p.X:F1}, {p.Y:F1})"));
        if (updateStatus && ReferenceEquals(SelectedImage, selected))
        {
            StatusMessage = string.IsNullOrWhiteSpace(selected.DiagnosticsText)
                ? rectified ? $"Loaded {selected.Record.FileName}: {selected.HoleCount} holes, {selected.NaturalCount} natural points." : $"Unable to rectify {selected.Record.FileName}."
                : $"Loaded with diagnostics: {selected.DiagnosticsText}";
        }
        RaisePropertyChanged(nameof(SelectedImage));
        RaisePropertyChanged(nameof(SelectedImageSummary));
    }

    private bool EnsureImageRectification(AnnotatedImageViewModel image, bool updatePreview)
    {
        image.AnnotationPoints.Clear();
        if (image.Record.IsImageMissing)
        {
            _rectificationByImage[image] = null;
            AddProcessingDiagnostic(image, $"Image file is missing: {image.Record.FileName}");
            RebuildHoleRows(image);
            return false;
        }

        try
        {
            var bitmap = image.OriginalImage;
            if (bitmap is null)
            {
                var loadResult = _workspaceService.LoadImageWithDetails(image.Record.ImagePath!);
                bitmap = loadResult.Image;
                image.OriginalImage = bitmap;
                if (loadResult.Orientation != LaserCollisionIn3DObjects.Domain.Imaging.ExifOrientation.Normal)
                {
                    AddProcessingDiagnostic(image, $"EXIF orientation {((ushort)loadResult.Orientation)} normalized: raw {loadResult.RawPixelWidth}x{loadResult.RawPixelHeight}, displayed {bitmap.PixelWidth}x{bitmap.PixelHeight}.");
                    AddCoordinateSystemDiagnostic(image, loadResult.RawPixelWidth, loadResult.RawPixelHeight, bitmap.PixelWidth, bitmap.PixelHeight);
                }
            }
            if (updatePreview)
            {
                image.OriginalOverlay = _workspaceService.CreateOriginalOverlay(image.Record, bitmap);
            }

            if (image.Record.Panel is null)
            {
                _rectificationByImage[image] = null;
                AddProcessingDiagnostic(image, "Panel annotation is missing.");
                RebuildHoleRows(image);
                return false;
            }

            if (image.Record.Panel.FittedQuadrilateralCorners.Count != 4)
            {
                _workspaceService.FitPanel(image.Record);
            }

            if (!_rectificationByImage.TryGetValue(image, out var rectification) || rectification is null)
            {
                rectification = _workspaceService.CreateRectification(image.Record, bitmap);
                _rectificationByImage[image] = rectification;
            }

            RebuildHoleRows(image);
            if (updatePreview)
            {
                image.WarpedImage = rectification?.WarpedImage;
                image.WarpedOverlay = rectification is null ? null : _workspaceService.CreateWarpedOverlay(rectification, rectification.WarpedImage);
            }

            if (rectification is null)
            {
                AddProcessingDiagnostic(image, "Panel rectification is unavailable.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _rectificationByImage[image] = null;
            RebuildHoleRows(image);
            AddProcessingDiagnostic(image, $"Image processing failed: {ex.Message}");
            if (updatePreview)
            {
                image.WarpedImage = null;
                image.WarpedOverlay = null;
            }
            return false;
        }
    }

    private async Task<bool> EnsureImageRectificationAsync(AnnotatedImageViewModel image, bool updatePreview)
    {
        image.AnnotationPoints.Clear();
        if (image.Record.IsImageMissing)
        {
            _rectificationByImage[image] = null;
            AddProcessingDiagnostic(image, $"Image file is missing: {image.Record.FileName}");
            RebuildHoleRows(image);
            return false;
        }

        try
        {
            var bitmap = image.OriginalImage;
            if (bitmap is null)
            {
                var loadResult = await Task.Run(() => _workspaceService.LoadImageWithDetails(image.Record.ImagePath!));
                bitmap = loadResult.Image;
                image.OriginalImage = bitmap;
                if (loadResult.Orientation != LaserCollisionIn3DObjects.Domain.Imaging.ExifOrientation.Normal)
                {
                    AddProcessingDiagnostic(image, $"EXIF orientation {((ushort)loadResult.Orientation)} normalized: raw {loadResult.RawPixelWidth}x{loadResult.RawPixelHeight}, displayed {bitmap.PixelWidth}x{bitmap.PixelHeight}.");
                    AddCoordinateSystemDiagnostic(image, loadResult.RawPixelWidth, loadResult.RawPixelHeight, bitmap.PixelWidth, bitmap.PixelHeight);
                }
            }

            if (image.Record.Panel is null)
            {
                _rectificationByImage[image] = null;
                AddProcessingDiagnostic(image, "Panel annotation is missing.");
                RebuildHoleRows(image);
                return false;
            }
            if (image.Record.Panel.FittedQuadrilateralCorners.Count != 4) _workspaceService.FitPanel(image.Record);

            if (!_rectificationByImage.TryGetValue(image, out var rectification) || rectification is null)
            {
                if (!bitmap.IsFrozen && bitmap.CanFreeze) bitmap.Freeze();
                rectification = bitmap.IsFrozen
                    ? await Task.Run(() => _workspaceService.CreateRectification(image.Record, bitmap))
                    : _workspaceService.CreateRectification(image.Record, bitmap);
                _rectificationByImage[image] = rectification;
            }

            RebuildHoleRows(image);
            if (updatePreview)
            {
                image.OriginalOverlay = _workspaceService.CreateOriginalOverlay(image.Record, bitmap);
                image.WarpedImage = rectification?.WarpedImage;
                image.WarpedOverlay = rectification is null ? null : _workspaceService.CreateWarpedOverlay(rectification, rectification.WarpedImage);
            }
            return rectification is not null;
        }
        catch (Exception ex)
        {
            _rectificationByImage[image] = null;
            RebuildHoleRows(image);
            AddProcessingDiagnostic(image, $"Image processing failed: {ex.Message}");
            if (updatePreview) { image.WarpedImage = null; image.WarpedOverlay = null; }
            return false;
        }
    }

    private static void AddProcessingDiagnostic(AnnotatedImageViewModel image, string message)
    {
        if (!image.Record.Diagnostics.Contains(message, StringComparer.Ordinal))
        {
            image.Record.Diagnostics.Add(message);
        }
    }

    private static void AddCoordinateSystemDiagnostic(AnnotatedImageViewModel image, int rawWidth, int rawHeight, int normalizedWidth, int normalizedHeight)
    {
        var annotationPoints = image.Record.Panel?.OriginalPolygonPoints.Concat(image.Record.Points.Select(static point => point.CenterPoint)).ToArray()
            ?? image.Record.Points.Select(static point => point.CenterPoint).ToArray();
        if (annotationPoints.Length == 0) return;

        static bool IsWithinBounds(Point point, int width, int height) => point.X >= 0 && point.X < width && point.Y >= 0 && point.Y < height;
        if (annotationPoints.Any(point => !IsWithinBounds(point, normalizedWidth, normalizedHeight))
            && annotationPoints.All(point => IsWithinBounds(point, rawWidth, rawHeight)))
        {
            AddProcessingDiagnostic(image, "Annotation coordinates fit the raw image bounds but not the EXIF-normalized bounds; verify that the VIA annotations were created against the visually oriented image.");
        }
    }

    private void RaiseCanExecuteChanges()
    {
        (SelectFolderCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SelectPreviousImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SelectNextImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyGlobalPanelDimensionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RelinkMissingFolderCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ImportPanelMeasurementsCsvCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (GenerateSceneCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void BeginBusy(string message)
    {
        BusyMessage = message;
        BusyProgressPercent = 0;
        IsBusyIndeterminate = true;
        IsBusy = true;
    }

    private void ReportBusyProgress(string stage, int current, int total)
    {
        BusyMessage = $"{stage} — {current} of {total}";
        BusyProgressPercent = total == 0 ? 100 : current * 100d / total;
        IsBusyIndeterminate = false;
    }

    private void EndBusy()
    {
        IsBusy = false;
        BusyMessage = string.Empty;
        BusyProgressPercent = 0;
        IsBusyIndeterminate = true;
    }

    private void ApplyGlobalPanelDimensions()
    {
        if (GlobalPanelWidthMm <= 0 || GlobalPanelHeightMm <= 0 || GlobalPanelThicknessMm <= 0)
        {
            StatusMessage = "Global panel width, height, and thickness must be greater than zero.";
            return;
        }

        foreach (var image in Images)
        {
            image.PanelWidthMm = GlobalPanelWidthMm;
            image.PanelHeightMm = GlobalPanelHeightMm;
            image.PanelThicknessMm = GlobalPanelThicknessMm;
            RebuildHoleRows(image);
        }

        StatusMessage = $"Applied {GlobalPanelWidthMm:F2}mm x {GlobalPanelHeightMm:F2}mm x {GlobalPanelThicknessMm:F2}mm to {Images.Count} images.";
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
    private async Task GenerateSceneAsync()
    {
        BeginBusy("Preparing scene...");
        try
        {
            var validationErrors = ValidateSceneGenerationInputs(includeRectification: false);
            if (validationErrors.Count > 0)
            {
                SetSceneGenerationFailure(validationErrors);
                return;
            }

            var failedImages = new List<string>();
            for (var i = 0; i < Images.Count; i++)
            {
                ReportBusyProgress("Rectifying panels", i + 1, Images.Count);
                if (!await EnsureImageRectificationAsync(Images[i], updatePreview: false)) failedImages.Add(Images[i].FileName);
            }
            validationErrors = failedImages.Count == 0 ? ValidateSceneGenerationInputs(includeRectification: true) : failedImages.Select(name => $"{name}: rectification could not be created.").ToList();
            if (validationErrors.Count > 0)
            {
                SetSceneGenerationFailure(validationErrors);
                return;
            }

            var methodology = SelectedPrismGenerationMethodology;
            var baseName = methodology == PrismGenerationMethodology.LtRtAnchoredOrthogonal
                ? "Annotation - LT-RT Anchored Orthogonal" : "Annotation - LT-Anchored 4-Point Best Fit";
            var sceneModel = new CollisionSceneViewModel(_sceneCollectionService?.CreateUniqueSceneName(baseName) ?? baseName);
            try
            {
                for (var index = 0; index < Images.Count; index++)
                {
                    var item = Images[index];
                    ReportBusyProgress("Generating scene", index + 1, Images.Count);
                    await Task.Yield();
                    var measuredCorners = ResolveMeasuredCornerWorldPoints(item);
                    var width = (float)(item.PanelWidthMm!.Value * .001);
                    var height = (float)(item.PanelHeightMm!.Value * .001);
                    var frame = MeasuredPanelFrameBuilder.Create(measuredCorners[0], measuredCorners[1], measuredCorners[2], measuredCorners[3], width, height, methodology);
                    sceneModel.Prisms.Add(CreatePrism(item, measuredCorners[0], frame));
                    foreach (var hole in CreateHolePoints(item, measuredCorners[0], frame)) sceneModel.HolePoints.Add(hole);
                    foreach (var point in CreateNaturalPoints(item, measuredCorners[0], frame)) sceneModel.NaturalPoints.Add(point);
                    foreach (var corner in measuredCorners) sceneModel.MeasuredCornerPoints.Add(new Point3(corner.X, corner.Y, corner.Z));
                    if (frame.Residuals is { } residuals) AddProcessingDiagnostic(item, $"4-point fit RMSE: {residuals.Rmse * 1000:F2} mm.");
                }
            }
            catch (ArgumentException ex) { SetSceneGenerationFailure(new[] { ex.Message }); return; }
            _sceneCollectionService?.AddScene(sceneModel);
            StatusMessage = $"Generated scene '{sceneModel.Name}'.";
        }
        finally { EndBusy(); }
    }

    private void SetSceneGenerationFailure(IReadOnlyList<string> errors)
    {
        var firstErrors = errors.Take(8).ToList();
        var suffix = errors.Count > firstErrors.Count ? $" | ...and {errors.Count - firstErrors.Count} more issue(s)." : string.Empty;
        StatusMessage = $"Scene generation readiness failed: {string.Join(" | ", firstErrors)}{suffix}";
        MessageBox.Show(StatusMessage);
    }


    private List<string> ValidateSceneGenerationInputs(bool includeRectification)
    {
        var errors = new List<string>();

        if (Images.Count == 0)
        {
            errors.Add("No annotated images are loaded.");
            return errors;
        }

        foreach (var image in Images)
        {
            if (!image.HasPanel)
            {
                errors.Add($"{image.FileName}: panel annotation is missing.");
            }
            if (image.Record.IsImageMissing)
            {
                errors.Add($"{image.FileName}: image file is missing.");
            }
            else if (image.HasPanel && image.Record.Panel!.FittedQuadrilateralCorners.Count != 4)
            {
                errors.Add($"{image.FileName}: panel fitting did not produce four corners.");
            }
            _rectificationByImage.TryGetValue(image, out var rectification);
            if (includeRectification && rectification is null)
            {
                errors.Add($"{image.FileName}: rectification is unavailable.");
            }
            if (includeRectification && rectification is not null && image.WarpedHoleCentersMm.Count != image.Record.Holes.Count())
            {
                errors.Add($"{image.FileName}: transformed hole coordinates could not be calculated.");
            }
            if (includeRectification && rectification is not null && image.WarpedNaturalCentersMm.Count != image.Record.NaturalPoints.Count())
                errors.Add($"{image.FileName}: transformed natural coordinates could not be calculated.");

            if (image.PanelWidthMm is null or <= 0)
            {
                errors.Add($"{image.FileName}: width is missing.");
            }

            if (image.PanelHeightMm is null or <= 0)
            {
                errors.Add($"{image.FileName}: height is missing.");
            }

            if (image.PanelThicknessMm is null or <= 0)
            {
                errors.Add($"{image.FileName}: thickness is missing.");
            }

            foreach (var corner in image.CornerMeasurements)
            {
                var prefix = $"{image.FileName} - {corner.DisplayName}";
                if (corner.SelectedMode == CornerMeasurementMode.Unspecified)
                {
                    errors.Add($"{prefix}: mode is not selected.");
                    continue;
                }

                if (corner.SelectedMode == CornerMeasurementMode.ManualMeasurement)
                {
                    if (corner.ManualAzimuthDeg is null)
                    {
                        errors.Add($"{prefix}: azimuth is missing.");
                    }

                    if (corner.ManualElevationDeg is null)
                    {
                        errors.Add($"{prefix}: elevation is missing.");
                    }

                    if (corner.ManualDistanceMeters is null)
                    {
                        errors.Add($"{prefix}: distance is missing.");
                    }
                }
                else if (corner.SelectedMode == CornerMeasurementMode.DirectCoordinateTheodolite)
                {
                    if (corner.DirectX is null)
                    {
                        errors.Add($"{prefix}: X coordinate is missing.");
                    }

                    if (corner.DirectY is null)
                    {
                        errors.Add($"{prefix}: Y coordinate is missing.");
                    }

                    if (corner.DirectZ is null)
                    {
                        errors.Add($"{prefix}: Z coordinate is missing.");
                    }
                }
            }
        }

        return errors;
    }

    private PrismItemViewModel CreatePrism(AnnotatedImageViewModel cornerMeasurement, Vector3 leftTop, MeasuredPanelFrame panelFrame)
    {
        Vector3 dimensions = new Vector3(
            (float)cornerMeasurement.PanelThicknessMm!.Value * 0.001f,
            (float)cornerMeasurement.PanelWidthMm!.Value * 0.001f,
            (float)cornerMeasurement.PanelHeightMm!.Value * 0.001f
            );
        // Measurements remain on the prism reference/mid-plane; thickness is deliberately not offset.
        Vector3 centerPoint = leftTop + panelFrame.Width * dimensions.Y / 2f + panelFrame.Down * dimensions.Z / 2f;
        
        PrismItemViewModel prism = new PrismItemViewModel();
        prism.PositionX = centerPoint.X; prism.PositionY = centerPoint.Y; prism.PositionZ = centerPoint.Z;
        prism.BaseOrientation = panelFrame.Orientation;
        prism.RotationX = 0; prism.RotationY = 0; prism.RotationZ = 0;
        prism.SizeX = dimensions.X; prism.SizeY = dimensions.Y; prism.SizeZ = dimensions.Z;

        return prism;
    }

    private static Vector3 ConvertToPointFromManual(double? azimuthDeg, double? elevationDeg, double? distance)
    {
        // All angles must be in degrees, distance in meters
        if (azimuthDeg != null && elevationDeg != null && distance != null)
        {
            Vector3 distVec = Vector3.UnitX * (float)distance;
            System.Numerics.Quaternion orientation = FrameOrientationBuilder.ApplyLocalZYXulerDegrees(System.Numerics.Quaternion.Identity, (float)azimuthDeg, (float)elevationDeg, 0);
            Vector3 final = Vector3.Transform(distVec, orientation);
            return final;
        }


        return new Vector3(float.NaN, float.NaN, float.NaN);
    }
    private static IReadOnlyList<Vector3> ResolveMeasuredCornerWorldPoints(AnnotatedImageViewModel image)
    {
        // The view model constructs this collection in the documented LT, RT, RB, LB order.
        return image.CornerMeasurements.Select(item => item.SelectedMode == CornerMeasurementMode.ManualMeasurement
            ? ConvertToPointFromManual(item.ManualAzimuthDeg, item.ManualElevationDeg, item.ManualDistanceMeters)
            : new Vector3((float)item.DirectX!.Value, (float)item.DirectY!.Value, (float)item.DirectZ!.Value)).ToArray();
    }

    private List<Point3> CreateHolePoints(AnnotatedImageViewModel cornerMeasurement, Vector3 leftTop, MeasuredPanelFrame panelFrame)
    {
        if (!_rectificationByImage.TryGetValue(cornerMeasurement, out var rectification) || rectification is null)
        {
            throw new InvalidOperationException($"No valid rectification is available for '{cornerMeasurement.FileName}'.");
        }
        if (cornerMeasurement.WarpedHoleCentersMm.Count != cornerMeasurement.Record.Holes.Count())
        {
            throw new InvalidOperationException($"Transformed hole coordinates are unavailable for '{cornerMeasurement.FileName}'.");
        }

        List<Point3> point3s = new List<Point3>();
        
        foreach (var item in cornerMeasurement.WarpedHoleCentersMm)
        {
           //Turn holes into 3D from 2D
           Vector3 transformed = leftTop
               + panelFrame.Width * (float)(item.X * 0.001)
               + panelFrame.Down * (float)(item.Y * 0.001);
           point3s.Add(new Point3(transformed.X, transformed.Y, transformed.Z));
        }
        return point3s;
    }

    private List<Point3> CreateNaturalPoints(AnnotatedImageViewModel image, Vector3 leftTop, MeasuredPanelFrame panelFrame)
        => CreateWorldPoints(image.WarpedNaturalCentersMm, leftTop, panelFrame);

    private static List<Point3> CreateWorldPoints(IEnumerable<Point> points, Vector3 leftTop, MeasuredPanelFrame panelFrame)
        => points.Select(item =>
        {
            var transformed = leftTop + panelFrame.Width * (float)(item.X * 0.001) + panelFrame.Down * (float)(item.Y * 0.001);
            return new Point3(transformed.X, transformed.Y, transformed.Z);
        }).ToList();

    public AnnotationWorkspaceState ExportWorkspaceState()
    {
        return new AnnotationWorkspaceState
        {
            FolderPath = SelectedFolderPath == "No folder selected." ? null : SelectedFolderPath,
            IsFolderResolved = IsFolderResolved,
            GlobalPanelWidthMm = GlobalPanelWidthMm,
            GlobalPanelHeightMm = GlobalPanelHeightMm,
            GlobalPanelThicknessMm = GlobalPanelThicknessMm,
            PrismGenerationMethodology = SelectedPrismGenerationMethodology.ToString(),
            Images = Images.Select(image => new AnnotationImageState
            {
                FileName = image.FileName,
                PanelWidthMm = image.PanelWidthMm,
                PanelHeightMm = image.PanelHeightMm,
                PanelThicknessMm = image.PanelThicknessMm,
                Corners = image.CornerMeasurements.Select(corner => new AnnotationCornerState
                {
                    CornerType = corner.CornerType.ToString(),
                    Mode = corner.SelectedMode.ToString(),
                    ManualAzimuthDeg = corner.ManualAzimuthDeg,
                    ManualElevationDeg = corner.ManualElevationDeg,
                    ManualDistanceMeters = corner.ManualDistanceMeters,
                    DirectX = corner.DirectX,
                    DirectY = corner.DirectY,
                    DirectZ = corner.DirectZ,
                }).ToList(),
            }).ToList(),
        };
    }

    public void ApplyWorkspaceState(AnnotationWorkspaceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        GlobalPanelWidthMm = state.GlobalPanelWidthMm;
        GlobalPanelHeightMm = state.GlobalPanelHeightMm;
        GlobalPanelThicknessMm = state.GlobalPanelThicknessMm;
        SelectedPrismGenerationMethodology = Enum.TryParse<PrismGenerationMethodology>(state.PrismGenerationMethodology, true, out var methodology)
            ? methodology : PrismGenerationMethodology.LtRtAnchoredOrthogonal;

        if (string.IsNullOrWhiteSpace(state.FolderPath))
        {
            StatusMessage = "Annotation state loaded without a folder reference.";
            IsFolderResolved = false;
            MissingFolderPath = null;
            return;
        }

        if (!AnnotationStateResolver.IsFolderResolved(state))
        {
            SelectedFolderPath = state.FolderPath;
            StatusMessage = $"Annotation folder is missing: {state.FolderPath}. Use Restore/Relink.";
            IsFolderResolved = false;
            MissingFolderPath = state.FolderPath;
            _pendingWorkspaceState = state;
            Images.Clear();
            return;
        }

        SelectedFolderPath = state.FolderPath;
        LoadProject(state.FolderPath);
        ApplyImageStateOverrides(state);
        _pendingWorkspaceState = null;
    }

    private async Task RestoreMissingFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Relink missing annotation folder",
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        await LoadProjectAsync(dialog.FolderName);

        if (_pendingWorkspaceState is not null)
        {
            ApplyImageStateOverrides(_pendingWorkspaceState);
            _pendingWorkspaceState = null;
        }
    }

    private void ApplyImageStateOverrides(AnnotationWorkspaceState state)
    {
        var imageLookup = state.Images.ToDictionary(image => image.FileName, StringComparer.OrdinalIgnoreCase);
        foreach (var image in Images)
        {
            if (!imageLookup.TryGetValue(image.FileName, out var persisted))
            {
                continue;
            }

            image.PanelWidthMm = persisted.PanelWidthMm;
            image.PanelHeightMm = persisted.PanelHeightMm;
            image.PanelThicknessMm = persisted.PanelThicknessMm;

            var cornersByType = persisted.Corners.ToDictionary(corner => corner.CornerType, StringComparer.OrdinalIgnoreCase);
            foreach (var corner in image.CornerMeasurements)
            {
                if (!cornersByType.TryGetValue(corner.CornerType.ToString(), out var persistedCorner))
                {
                    continue;
                }

                if (Enum.TryParse<CornerMeasurementMode>(persistedCorner.Mode, ignoreCase: true, out var mode))
                {
                    corner.SelectedMode = mode;
                }

                corner.ManualAzimuthDeg = persistedCorner.ManualAzimuthDeg;
                corner.ManualElevationDeg = persistedCorner.ManualElevationDeg;
                corner.ManualDistanceMeters = persistedCorner.ManualDistanceMeters;
                corner.DirectX = persistedCorner.DirectX;
                corner.DirectY = persistedCorner.DirectY;
                corner.DirectZ = persistedCorner.DirectZ;
            }

            RebuildHoleRows(image);
        }
    }

    private void RebuildHoleRows(AnnotatedImageViewModel image)
    {
        image.AnnotationPoints.Clear();
        image.WarpedHoleCentersMm.Clear();
        image.WarpedNaturalCentersMm.Clear();
        _rectificationByImage.TryGetValue(image, out var rectification);
        var canConvertToMm = rectification is not null
            && image.Record.Calibration.IsConfigured
            && rectification.DestinationSizePixels.Width > 0
            && rectification.DestinationSizePixels.Height > 0;
        var mmScaleX = canConvertToMm ? image.Record.Calibration.PhysicalWidthMm!.Value / rectification!.DestinationSizePixels.Width : 0d;
        var mmScaleY = canConvertToMm ? image.Record.Calibration.PhysicalHeightMm!.Value / rectification!.DestinationSizePixels.Height : 0d;

        foreach (var row in AnnotationWorkspaceService.BuildAnnotationPointRows(image.Record, rectification, image.Record.Calibration))
        {
            image.AnnotationPoints.Add(row);
        }

        if (canConvertToMm)
        {
            foreach (var point in rectification!.TransformedHoleCenters)
            {
                image.WarpedHoleCentersMm.Add(new Point(point.X * mmScaleX, point.Y * mmScaleY));
            }
            foreach (var point in rectification.TransformedNaturalCenters)
                image.WarpedNaturalCentersMm.Add(new Point(point.X * mmScaleX, point.Y * mmScaleY));
        }

        RaisePropertyChanged(nameof(WarpedHoleCentersMmByImage));
    }
}
