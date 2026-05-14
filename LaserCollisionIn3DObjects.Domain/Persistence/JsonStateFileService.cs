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

        var json = JsonSerializer.Serialize(state, _jsonOptions);
        File.WriteAllText(filePath, json);
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

        return state;
    }
}
