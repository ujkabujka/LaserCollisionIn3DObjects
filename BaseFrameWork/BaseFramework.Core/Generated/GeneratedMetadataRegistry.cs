using System.Collections.Concurrent;
using BaseFramework.Core.Metadata;

namespace BaseFramework.Core.Generated;

public static class GeneratedMetadataRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<InspectableTypeMetadata>> _builders = new();

    public static void Register(Type targetType, Func<InspectableTypeMetadata> builder)
    {
        _builders[targetType] = builder;
    }

    public static bool TryCreate(Type targetType, out InspectableTypeMetadata metadata)
    {
        if (_builders.TryGetValue(targetType, out var builder))
        {
            metadata = builder();
            return true;
        }

        metadata = default!;
        return false;
    }
}
