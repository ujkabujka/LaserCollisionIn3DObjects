using BaseFramework.Core.Metadata;

namespace BaseFramework.Core.Services;

public interface IObjectMetadataProvider
{
    InspectableTypeMetadata GetMetadata(object target);
    InspectableTypeMetadata GetMetadata(Type targetType);
}
