using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
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
    private readonly Dictionary<AnnotatedImageViewModel, RectificationResult?> _rectificationByImage = new();
    private string _selectedFolderPath = "No folder selected.";
    private string _statusMessage = "Select an annotation folder to begin.";
    private AnnotatedImageViewModel? _selectedImage;
    private double _globalPanelWidthMm = 1000;
    private double _globalPanelHeightMm = 1000;
    private double _globalPanelThicknessMm = 10;

    public AnnotationWorkspaceViewModel(SceneCollectionService? sceneCollectionService = null)
    {
        _sceneCollectionService = sceneCollectionService;
        SelectFolderCommand = new RelayCommand(SelectFolder);
        SelectPreviousImageCommand = new RelayCommand(SelectPreviousImage, () => SelectedImageIndex > 0);
        SelectNextImageCommand = new RelayCommand(SelectNextImage, () => SelectedImageIndex >= 0 && SelectedImageIndex < Images.Count - 1);
        ApplyGlobalPanelDimensionsCommand = new RelayCommand(ApplyGlobalPanelDimensions, () => Images.Count > 0);
        GenerateSceneCommand = new RelayCommand(GenerateScene, () => Images.Count > 0);
    }

    public ICommand SelectFolderCommand { get; }

    public ICommand SelectPreviousImageCommand { get; }

    public ICommand SelectNextImageCommand { get; }

    public ICommand ApplyGlobalPanelDimensionsCommand { get; }

    public ICommand GenerateSceneCommand { get; }

    public ObservableCollection<AnnotatedImageViewModel> Images { get; } = new();

    public CornerMeasurementMode[] CornerMeasurementModes { get; } = Enum.GetValues<CornerMeasurementMode>();

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

    public double GlobalPanelThicknessMm
    {
        get => _globalPanelThicknessMm;
        set => SetProperty(ref _globalPanelThicknessMm, value);
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
                    PanelThicknessMm = GlobalPanelThicknessMm,
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
        (GenerateSceneCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
    private void GenerateScene()
    {
        if (GenerateSceneReadinessCheck())
        {
            // Create scene at first
            CollisionSceneViewModel sceneModel = new CollisionSceneViewModel("Annotation scene");

            // First for all panels create the prisms accordingly
            // Also create holes for all images
            foreach (var item in Images)
            {
                // Create prisms
                PrismItemViewModel prism = CreatePrism(item);
                sceneModel.Prisms.Add(prism);

                // Create Holes
                List<Point3> holes = CreateHolePoints(item);
                foreach (var hole in holes)
                {
                    sceneModel.HolePoints.Add(hole);
                }

            }

            _sceneCollectionService?.AddScene(sceneModel);
        
        }
        else
            MessageBox.Show(StatusMessage);
    }

    private bool GenerateSceneReadinessCheck()
    {
        var errors = ValidateSceneGenerationInputs();
        if (errors.Count == 0)
        {
            StatusMessage = "All required panel measurements are provided. Scene generation can proceed.";
            return true;
        }

        var firstErrors = errors.Take(8).ToList();
        var suffix = errors.Count > firstErrors.Count
            ? $" | ...and {errors.Count - firstErrors.Count} more issue(s)."
            : string.Empty;
        StatusMessage = $"Scene generation readiness failed: {string.Join(" | ", firstErrors)}{suffix}";
        return false;
    }

    private List<string> ValidateSceneGenerationInputs()
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

    private PrismItemViewModel CreatePrism(AnnotatedImageViewModel cornerMeasurement)
    {
        Vector3 dimensions = new Vector3(
            (float)cornerMeasurement.PanelThicknessMm * 0.001f, 
            (float)cornerMeasurement.PanelWidthMm * 0.001f, 
            (float)cornerMeasurement.PanelHeightMm * 0.001f
            );
        // From left top to counterclockwise
        List<Vector3> cornerPoints = new List<Vector3>();
        foreach (var item in cornerMeasurement.CornerMeasurements)
        {
            if(item.SelectedMode == CornerMeasurementMode.ManualMeasurement)
                cornerPoints.Add(convertToPointFromManuel(item.ManualAzimuthDeg, item.ManualElevationDeg, item.ManualDistanceMeters));
            else
                cornerPoints.Add(new Vector3((float)item.DirectX, (float)item.DirectY, (float)item.DirectZ));
        }

        // From this points we will find all the prism locations pos, oriantation, size
        Vector3 vec_x = cornerPoints[1] - cornerPoints[0];
        Vector3 vec_y = cornerPoints[3] - cornerPoints[0];

        vec_x = Vector3.Normalize(vec_x);
        vec_y = Vector3.Normalize(vec_y);
        Vector3 vec_z = Vector3.Cross(vec_x, vec_y);

        Vector3 centerPoint = cornerPoints[0] + vec_x * dimensions.Y / 2f + vec_z * dimensions.X / 2f + vec_y * dimensions.Z / 2f;
        //For the panel frame things are different
        // u vector is - vec_Z and v vector is -vec_y
        Vector3 u = -vec_z;
        Vector3 v = -vec_y;
        float x_angle = MathF.Atan2(v.Y, v.Z);
        float y_angle = MathF.Asin(-v.X);
        float z_angle = MathF.Atan2(v.Y * u.Z - v.Z * u.Y, u.X);

        x_angle = FrameOrientationBuilder.RadiansToDegrees(x_angle);
        y_angle = FrameOrientationBuilder.RadiansToDegrees(y_angle);
        z_angle = FrameOrientationBuilder.RadiansToDegrees(z_angle);
        
        PrismItemViewModel prism = new PrismItemViewModel();
        prism.PositionX = centerPoint.X; prism.PositionY = centerPoint.Y; prism.PositionZ = centerPoint.Z;
        prism.RotationX = x_angle; prism.RotationY = y_angle; prism.RotationZ = z_angle;
        prism.SizeX = dimensions.X; prism.SizeY = dimensions.Y; prism.SizeZ = dimensions.Z;

        return prism;
    }

    private Vector3 convertToPointFromManuel(double? azimuthDeg, double? elevationDeg, double? distance)
    {
        // All angles must be in degrees, distance in meters
        if (azimuthDeg != null && elevationDeg != null && distance != null)
        {
            Vector3 distVec = Vector3.UnitX * (float)distance;
            System.Numerics.Quaternion orientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(System.Numerics.Quaternion.Identity, (float)azimuthDeg, (float)elevationDeg, 0);
            Vector3 final = Vector3.Transform(distVec, orientation);
            return final;
        }


        return new Vector3(float.NaN, float.NaN, float.NaN);
    }
    private List<Point3> CreateHolePoints(AnnotatedImageViewModel cornerMeasurement)
    {
        //////////////////////
        // From left top to counterclockwise
        List<Vector3> cornerPoints = new List<Vector3>();
        foreach (var item in cornerMeasurement.CornerMeasurements)
        {
            if(item.SelectedMode == CornerMeasurementMode.ManualMeasurement)
                cornerPoints.Add(convertToPointFromManuel(item.ManualAzimuthDeg, item.ManualElevationDeg, item.ManualDistanceMeters));
            else
                cornerPoints.Add(new Vector3((float)item.DirectX, (float)item.DirectY, (float)item.DirectZ));
        }

        // From this points we will find all the prism locations pos, oriantation, size
        Vector3 vec_x = cornerPoints[1] - cornerPoints[0];
        Vector3 vec_y = cornerPoints[3] - cornerPoints[0];

        vec_x = Vector3.Normalize(vec_x);
        vec_y = Vector3.Normalize(vec_y);
        Vector3 vec_z = Vector3.Cross(vec_x, vec_y);
        //////////////////////////////////////////////
        // Build rotation matrix from absolute to prism. Note that transpose of the matrix is true
        // Matrix4x4 rotationMatrix = new Matrix4x4(
        //     vec_x.X, vec_z.Y * vec_x.Z - vec_z.Z * vec_x.Y, vec_z.X, -cornerPoints[0].X,
        //     vec_x.Y, vec_z.Z * vec_x.X - vec_z.X * vec_x.Z, vec_z.Y, -cornerPoints[0].Y,
        //     vec_x.Z, vec_z.X * vec_x.Y - vec_z.Y * vec_x.X, vec_z.Z, -cornerPoints[0].Z,
        //     0,0,0,1
        // );

        Matrix4x4 rotationMatrix = new Matrix4x4(
            vec_x.X, vec_x.Y, vec_x.Z, 0,
            vec_z.Y * vec_x.Z - vec_z.Z * vec_x.Y, vec_z.Z * vec_x.X - vec_z.X * vec_x.Z, vec_z.X * vec_x.Y - vec_z.Y * vec_x.X, 0,
            vec_z.X, vec_z.Y, vec_z.Z, 0,
            cornerPoints[0]. X,cornerPoints[0].Y, cornerPoints[0].Z, 1
        );
        
        var record = _rectificationByImage[cornerMeasurement];
        List<Point3> point3s = new List<Point3>();
        
        foreach (var item in cornerMeasurement.WarpedHoleCentersMm)
        {
           //Turn holes into 3D from 2D
           Vector3 hole = new Vector3((float)(item.X * 0.001), (float)(item.Y * 0.001), 0);
            
           //Rotate the frame to absolute coordinate
           Vector3 transformed = Vector3.Transform(hole, rotationMatrix);

           // Translate the coordinate to the absolute axis
           point3s.Add(new Point3(transformed.X, transformed.Y, transformed.Z));
        }
        return point3s;
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
}
