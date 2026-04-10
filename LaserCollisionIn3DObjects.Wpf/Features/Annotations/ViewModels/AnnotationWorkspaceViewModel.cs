using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using System.Windows.Input;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;

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
    }

    public ICommand SelectFolderCommand { get; }

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
            if (SetProperty(ref _selectedImage, value) && value is not null)
            {
                ProcessSelectedImage(value);
            }
        }
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
                    _workspaceService.FitPanel(record);
                }

                Images.Add(new AnnotatedImageViewModel { Record = record });
            }

            StatusMessage = $"Loaded {Images.Count} image annotations from {Path.GetFileName(project.JsonFilePath)}.";
            SelectedImage = Images.FirstOrDefault();
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
                selected.WarpedOverlay = _workspaceService.CreateWarpedOverlay(rectified);

                for (var i = 0; i < selected.Record.Holes.Count; i++)
                {
                    var hole = selected.Record.Holes[i];
                    var warpedCenter = rectified.TransformedHoleCenters[i];
                    selected.Holes.Add(new HoleViewModel
                    {
                        Index = i + 1,
                        ShapeType = hole.ShapeType,
                        OriginalCenter = $"({hole.CenterPoint.X:F1}, {hole.CenterPoint.Y:F1})",
                        WarpedCenter = $"({warpedCenter.X:F1}, {warpedCenter.Y:F1})",
                        PixelArea = hole.PixelArea.ToString("F2"),
                    });
                }
            }

            selected.PanelCornersText = selected.Record.Panel is null
                ? "No panel"
                : string.Join("; ", selected.Record.Panel.FittedQuadrilateralCorners.Select(static p => $"({p.X:F1}, {p.Y:F1})"));

            StatusMessage = $"Loaded {selected.Record.FileName}: {selected.Record.Holes.Count} holes.";
            RaisePropertyChanged(nameof(SelectedImage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to process selected image: {ex.Message}";
        }
    }
}
