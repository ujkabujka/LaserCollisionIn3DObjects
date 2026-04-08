using BaseFramework.Core.Metadata;

namespace BaseFramework.Core.Services;

public interface IRuntimeInspectableMetadataSource
{
    InspectableTypeMetadata GetRuntimeMetadata();
}
