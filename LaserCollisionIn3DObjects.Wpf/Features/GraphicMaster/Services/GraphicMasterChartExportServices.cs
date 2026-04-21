using Microsoft.Win32;
using OxyPlot.Wpf;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.Services;

public interface IGraphicMasterSaveFileDialogService
{
    string? SelectPngPath();
}

public interface IGraphicMasterPngExportService
{
    void ExportVisiblePlot(PlotView plotView, string filePath);
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
    public void ExportVisiblePlot(PlotView plotView, string filePath)
    {
        ArgumentNullException.ThrowIfNull(plotView);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        plotView.UpdateLayout();

        var width = Math.Max(1, (int)Math.Ceiling(plotView.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(plotView.ActualHeight));

        var render = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        render.Render(plotView);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(render));

        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }
}
