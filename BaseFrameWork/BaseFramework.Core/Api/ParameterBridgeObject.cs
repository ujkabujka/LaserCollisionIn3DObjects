using System.Reflection;
using BaseFramework.Core.Attributes;

namespace BaseFramework.Core.Api;

public abstract class ParameterBridgeObject : ObservableObject
{
    public IReadOnlyDictionary<string, object?> GetParameters(ParameterExportMode exportMode = ParameterExportMode.StableKey)
    {
        var values = new Dictionary<string, object?>();

        foreach (var metadata in GetInspectableProperties())
        {
            var key = exportMode == ParameterExportMode.ClrName ? metadata.Property.Name : metadata.Attribute.Key;
            values[key] = metadata.Property.GetValue(this);
        }

        return values;
    }

    public IReadOnlyDictionary<string, object?> GetParametersByClrName()
        => GetParameters(ParameterExportMode.ClrName);

    public void SetParameters(IReadOnlyDictionary<string, object?> values)
    {
        var properties = GetInspectableProperties()
            .Where(item => item.Property.CanWrite)
            .SelectMany(item => new[]
            {
                new KeyValuePair<string, PropertyInfo>(item.Attribute.Key, item.Property),
                new KeyValuePair<string, PropertyInfo>(item.Property.Name, item.Property)
            })
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in values)
        {
            if (!properties.TryGetValue(pair.Key, out var property))
            {
                continue;
            }

            property.SetValue(this, pair.Value);
        }
    }

    private IEnumerable<(PropertyInfo Property, InspectableMemberAttribute Attribute)> GetInspectableProperties()
        => GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<InspectableMemberAttribute>()))
            .Where(item => item.Attribute is not null)
            .Select(item => (item.Property, item.Attribute!));
}
