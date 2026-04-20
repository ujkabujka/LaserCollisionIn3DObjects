using LaserCollisionIn3DObjects.Domain.Graphing;

namespace LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;

public sealed class GraphTypeOptionViewModel
{
    public required IGraphType GraphType { get; init; }
    public string DisplayName => GraphType.DisplayName;
}
