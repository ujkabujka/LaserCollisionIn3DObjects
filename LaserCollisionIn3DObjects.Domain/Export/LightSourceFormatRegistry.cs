using System.Reflection;

namespace LaserCollisionIn3DObjects.Domain.Export;

/// <summary>Discovers concrete parameterless <see cref="ILightSourceFormat"/> classes in compiled assemblies.</summary>
public sealed class LightSourceFormatRegistry
{
    public LightSourceFormatRegistry(IEnumerable<Assembly>? assemblies = null)
    {
        var sources = assemblies?.ToArray() ?? new[] { typeof(ILightSourceFormat).Assembly };
        Formats = sources.SelectMany(GetLoadableTypes)
            .Where(type => typeof(ILightSourceFormat).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false } && type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (ILightSourceFormat)Activator.CreateInstance(type)!)
            .OrderByDescending(format => format.Id == "canonical-text-v1")
            .ThenBy(format => format.DisplayName, StringComparer.Ordinal)
            .ToArray();
        Validate(Formats);
    }
    public LightSourceFormatRegistry(IEnumerable<ILightSourceFormat> formats)
    {
        Formats = formats.OrderByDescending(format => format.Id == "canonical-text-v1").ThenBy(format => format.DisplayName, StringComparer.Ordinal).ToArray();
        Validate(Formats);
    }

    public IReadOnlyList<ILightSourceFormat> Formats { get; }
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type is not null)!; }
    }
    private static void Validate(IEnumerable<ILightSourceFormat> formats)
    {
        foreach (var format in formats)
        {
            if (string.IsNullOrWhiteSpace(format.Id)) throw new InvalidOperationException("A light-source format has no ID.");
            if (string.IsNullOrWhiteSpace(format.DisplayName)) throw new InvalidOperationException($"Light-source format '{format.Id}' has no display name.");
            if (format.FileExtensions.Count == 0) throw new InvalidOperationException($"Light-source format '{format.Id}' has no file extensions.");
        }
        var duplicate = formats.GroupBy(format => format.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Duplicate light-source format ID: '{duplicate.Key}'.");
    }
}
