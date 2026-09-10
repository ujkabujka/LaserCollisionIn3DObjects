using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaserCollisionIn3DObjects.Domain.Persistence;

public sealed class JsonStateFileService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public void SaveProject(string filePath, ProjectState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        state.SchemaVersion = ProjectState.CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        AtomicWrite(filePath, json);
    }

    public ProjectState LoadProject(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<ProjectState>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Could not deserialize project state file.");

        if (state.SchemaVersion <= 0)
        {
            throw new InvalidOperationException("Project state schemaVersion must be positive.");
        }

        if (state.SchemaVersion > ProjectState.CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Project schema version {state.SchemaVersion} is newer than the supported version {ProjectState.CurrentSchemaVersion}.");
        }

        // Version 1 omitted Graphic Master data. Property initializers supply its empty migration.
        state.Scenes ??= new();
        state.AvailableSources ??= new();
        state.CollisionWorkspace ??= new();
        state.ProjectionWorkspace ??= new();
        state.AnnotationWorkspace ??= new();
        state.GraphicMaster ??= new();
        state.GraphicMaster.ImportedSources ??= new();
        state.GraphicMaster.StoredCharts ??= new();
        state.SchemaVersion = ProjectState.CurrentSchemaVersion;
        return state;
    }

    public Task SaveProjectAsync(string filePath, ProjectState state, CancellationToken cancellationToken = default) =>
        Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); SaveProject(filePath, state); }, cancellationToken);

    public Task<ProjectState> LoadProjectAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); return LoadProject(filePath); }, cancellationToken);

    private static void AtomicWrite(string filePath, string contents)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
