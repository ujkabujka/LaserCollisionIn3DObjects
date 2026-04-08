using BaseFramework.Core;
using BaseFramework.Core.Access;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.Wpf.Controls;

public sealed record InspectorEditorContext(
    ObservableObject Target,
    InspectableMemberMetadata Member,
    IObjectMetadataProvider MetadataProvider,
    IMemberAccessEvaluator AccessEvaluator,
    InspectableAccessContext AccessContext,
    IInspectorEditorRegistry EditorRegistry);
