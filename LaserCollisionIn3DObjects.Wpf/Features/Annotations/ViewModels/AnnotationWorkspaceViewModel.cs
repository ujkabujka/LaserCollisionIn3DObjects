using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using System.Windows.Media.Imaging;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

public sealed class AnnotationWorkspaceViewModel : ObservableObject
{
    private readonly AnnotationWorkspaceService _workspaceService = new();
    private string _selectedFolderPath = "No folder selected.";
    private string _statusMessage = "Select an annotation folder to begin.";
    private AnnotatedImageViewModel? _selectedImage;

    public AnnotationWorkspaceViewModel()
    {
        SelectFolderCommand = new RelayCommand(SelectFolder);
        SelectPreviousImageCommand = new RelayCommand(SelectPreviousImage, () => SelectedImageIndex > 0);
        SelectNextImageCommand = new RelayCommand(SelectNextImage, () => SelectedImageIndex >= 0 && SelectedImageIndex < Images.Count - 1);
    }

    public ICommand SelectFolderCommand { get; }

    public ICommand SelectPreviousImageCommand { get; }

    public ICommand SelectNextImageCommand { get; }

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

                Images.Add(new AnnotatedImageViewModel { Record = record });
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

            foreach (var row in AnnotationWorkspaceService.BuildHoleRows(selected.Record, rectified))
            {
                selected.Holes.Add(row);
            }

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
