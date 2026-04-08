using BaseFramework.Core;
using BaseFramework.Core.Attributes;
using BaseFramework.Core.Collections;
using BaseFramework.Core.Api;

namespace BaseFramework.WpfHost.Models;

public enum NodeMode
{
    Draft,
    Ready,
    Disabled
}

public sealed class DemoChildNode : ParameterBridgeObject
{
    [InspectableMember("child.weight", "Child Weight", Order = 1)]
    public double Weight
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("child.count", "Child Count", Order = 2)]
    public int Count
    {
        get => Get<int>();
        set => Set(value);
    }

    protected override void OnUpdate()
    {
    }
}

[GenerateInspectorMetadata]
public sealed class DemoNode : ParameterBridgeObject
{
    public DemoNode()
    {
        Threshold = 10;
        Ratio = 1.5;
        Mode = NodeMode.Draft;
        Child = new DemoChildNode();
        Items = new ObservableChildrenCollection<DemoChildNode>
        {
            new() { Weight = 2.5, Count = 4 },
            new() { Weight = 5.1, Count = 1 }
        };
    }

    [InspectableMember("node.threshold", "Threshold", Order = 1)]
    public int Threshold
    {
        get => Get<int>();
        set => Set(value);
    }

    [InspectableMember("node.ratio", "Ratio", Order = 2)]
    public double Ratio
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("node.mode", "Mode", Order = 3)]
    public NodeMode Mode
    {
        get => Get<NodeMode>();
        set => Set(value);
    }

    [InspectableMember("node.child", "Child Node", Order = 4)]
    public DemoChildNode Child
    {
        get => Get<DemoChildNode>();
        set => Set(value);
    }

    [InspectableMember("node.items", "Child Collection", Order = 5)]
    public ObservableChildrenCollection<DemoChildNode> Items
    {
        get => Get<ObservableChildrenCollection<DemoChildNode>>();
        set => Set(value);
    }

    [InspectableMember("node.bump", "Increase Ratio", Order = 6)]
    public void IncreaseRatio(double delta)
    {
        Ratio += delta;
    }

    protected override void OnUpdate()
    {
    }
}
