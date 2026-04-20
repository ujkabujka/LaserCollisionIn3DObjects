using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Wpf;

namespace LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.Services;

public interface IGraphicMasterSaveFileDialogService
{
    string? SelectPngPath();
}

public interface IGraphicMasterPngExportService
{
    void Export(PlotModel plotModel, string filePath, int width, int height);
}

public sealed class GraphicMasterSaveFileDialogService : IGraphicMasterSaveFileDialogService
{
    public string? SelectPngPath()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = "chart.png",
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

public sealed class GraphicMasterPngExportService : IGraphicMasterPngExportService
{
    public void Export(PlotModel plotModel, string filePath, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(plotModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var exporter = new PngExporter
        {
            Width = Math.Max(1, width),
            Height = Math.Max(1, height),
            Resolution = 96,
        };

        exporter.ExportToFile(plotModel, filePath);
    }
}
