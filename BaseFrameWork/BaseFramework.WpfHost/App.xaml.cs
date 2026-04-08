using System.IO;
using System.Text;
using System.Windows.Threading;

namespace BaseFramework.WpfHost;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        MessageBox.Show(
            $"WPF host beklenmeyen bir hatayla karşılaştı:\n{e.Exception}",
            "BaseFramework.WpfHost Hata",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog(ex);
        }
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BaseFramework", "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"wpf-crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, new StringBuilder().AppendLine(exception.ToString()).ToString());
        }
        catch
        {
            // no-op
        }
    }
}
