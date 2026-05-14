namespace LaserCollisionIn3DObjects.Wpf.Services;

public sealed class ApplicationLogEntry
{
    public required DateTime Timestamp { get; init; }
    public required ApplicationLogLevel Level { get; init; }
    public required string Message { get; init; }
    public string? Source { get; init; }
    public string? ExceptionText { get; init; }
}
