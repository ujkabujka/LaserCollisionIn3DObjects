using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace LaserCollisionIn3DObjects.Wpf.Services;

public sealed class ApplicationLogService
{
    private readonly ObservableCollection<ApplicationLogEntry> _entries = new();
    private readonly Dispatcher? _dispatcher;

    public ApplicationLogService(int maxEntryCount = 2000, Dispatcher? dispatcher = null)
    {
        MaxEntryCount = maxEntryCount > 0 ? maxEntryCount : throw new ArgumentOutOfRangeException(nameof(maxEntryCount));
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher;
    }

    public int MaxEntryCount { get; }

    public ObservableCollection<ApplicationLogEntry> Entries => _entries;

    public void LogTrace(string message, string? source = null) => Log(ApplicationLogLevel.Trace, message, source);
    public void LogInfo(string message, string? source = null) => Log(ApplicationLogLevel.Info, message, source);
    public void LogSuccess(string message, string? source = null) => Log(ApplicationLogLevel.Success, message, source);
    public void LogWarning(string message, string? source = null) => Log(ApplicationLogLevel.Warning, message, source);

    public void LogError(string message, Exception? exception = null, string? source = null)
        => Log(ApplicationLogLevel.Error, message, source, exception);

    public void Clear()
    {
        InvokeOnUiThread(_entries.Clear);
    }

    public string CopyAllText()
    {
        var builder = new StringBuilder();
        foreach (var entry in _entries)
        {
            builder.Append('[')
                .Append(entry.Timestamp.ToString("HH:mm:ss"))
                .Append("] [")
                .Append(entry.Level)
                .Append(']');

            if (!string.IsNullOrWhiteSpace(entry.Source))
            {
                builder.Append(' ').Append('(').Append(entry.Source).Append(')');
            }

            builder.Append(' ').Append(entry.Message);

            if (!string.IsNullOrWhiteSpace(entry.ExceptionText))
            {
                builder.Append(" | ").Append(entry.ExceptionText);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void Log(ApplicationLogLevel level, string message, string? source = null, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var entry = new ApplicationLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source,
            ExceptionText = exception?.ToString(),
        };

        InvokeOnUiThread(() =>
        {
            _entries.Add(entry);
            while (_entries.Count > MaxEntryCount)
            {
                _entries.RemoveAt(0);
            }
        });
    }

    private void InvokeOnUiThread(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.BeginInvoke(action);
    }
}
