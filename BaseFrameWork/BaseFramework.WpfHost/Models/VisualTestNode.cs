using BaseFramework.Core;
using BaseFramework.Core.Attributes;
using BaseFramework.Core.Collections;
using BaseFramework.Core.Api;

namespace BaseFramework.WpfHost.Models;

[GenerateInspectorMetadata]
public sealed class VisualTestNode : ParameterBridgeObject
{
    public VisualTestNode()
    {
        Intensity = 3;
        Scale = 12.5;
        Enabled = true;
        Mode = NodeMode.Ready;
        Child = new DemoChildNode { Count = 5, Weight = 8.2 };
        Children = new ObservableChildrenCollection<DemoChildNode>
        {
            new() { Count = 1, Weight = 1.1 },
            new() { Count = 2, Weight = 2.2 }
        };
    }

    [InspectableMember("visual.intensity", "Intensity", Order = 1)]
    public int Intensity
    {
        get => Get<int>();
        set => Set(value);
    }

    [InspectableMember("visual.scale", "Scale", Order = 2)]
    public double Scale
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("visual.enabled", "Enabled", Order = 3)]
    public bool Enabled
    {
        get => Get<bool>();
        set => Set(value);
    }

    [InspectableMember("visual.mode", "Mode", Order = 4)]
    public NodeMode Mode
    {
        get => Get<NodeMode>();
        set => Set(value);
    }

    [InspectableMember("visual.child", "Child", Order = 5)]
    public DemoChildNode Child
    {
        get => Get<DemoChildNode>();
        set => Set(value);
    }

    [InspectableMember("visual.children", "Children", Order = 6)]
    public ObservableChildrenCollection<DemoChildNode> Children
    {
        get => Get<ObservableChildrenCollection<DemoChildNode>>();
        set => Set(value);
    }

    [InspectableMember("visual.apply", "Apply Parameters", Order = 7)]
    public void ApplyParameters(double delta, int repeat, bool invert, NodeMode mode, string note)
    {
        Scale += delta;
        Intensity += repeat;
        Enabled = invert ? !Enabled : Enabled;
        Mode = mode;
    }

    protected override void OnUpdate()
    {
    }
}
