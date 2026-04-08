using System.Collections.Generic;
using System.Linq;
using BaseFramework.Core;
using BaseFramework.Core.Api;
using BaseFramework.Core.Attributes;
using BaseFramework.Core.Services;
using BaseFramework.Core.UndoRedo;
using BaseFramework.Core.Notes;

namespace BaseFramework.Core.Tests;

public class ObservableObjectTests
{
    [Fact]
    public void Set_ShouldTriggerDependentUpdate()
    {
        var parent = new UpdateProbeNode();
        var child = new UpdateProbeNode();
        child.AddDependency(parent);

        parent.Probe = 10;

        Assert.Equal(1, parent.UpdateCount);
        Assert.Equal(1, child.UpdateCount);
        Assert.True(child.LastUpdateOrder > parent.LastUpdateOrder);
    }

    [Fact]
    public void Get_ShouldUpdateDependenciesBeforeOwner()
    {
        var dependency = new UpdateProbeNode();
        var owner = new UpdateProbeNode();
        owner.AddDependency(dependency);

        owner.Touch();
        dependency.Touch();

        _ = owner.Probe;

        Assert.True(dependency.UpdateCount >= 1);
        Assert.True(owner.UpdateCount >= 1);
        Assert.True(dependency.LastUpdateOrder < owner.LastUpdateOrder);
    }

    [Fact]
    public void UndoRedo_ShouldRestorePreviousValue()
    {
        var manager = new UndoRedoManager();
        var node = new TestNode(manager);

        node.Value = 2;
        node.Value = 5;

        manager.Undo();
        Assert.Equal(2, node.Value);

        manager.Redo();
        Assert.Equal(5, node.Value);
    }

    [Fact]
    public void Transaction_ShouldUndoAsSingleBatch()
    {
        var manager = new UndoRedoManager();
        var node = new TestNode(manager);

        manager.BeginTransaction();
        node.Value = 11;
        node.Secondary = 22;
        manager.CommitTransaction();

        Assert.Equal(11, node.Value);
        Assert.Equal(22, node.Secondary);

        manager.Undo();
        Assert.Equal(0, node.Value);
        Assert.Equal(0, node.Secondary);

        manager.Redo();
        Assert.Equal(11, node.Value);
        Assert.Equal(22, node.Secondary);
    }

    [Fact]
    public void Bridge_GetSetParameters_ShouldOnlyUseInspectableMembers()
    {
        var node = new BridgeProbeNode
        {
            VisibleValue = 7,
            HiddenValue = 99
        };

        var parameters = node.GetParameters();
        Assert.True(parameters.ContainsKey("visible"));
        Assert.False(parameters.ContainsKey(nameof(BridgeProbeNode.VisibleValue)));
        Assert.False(parameters.ContainsKey(nameof(BridgeProbeNode.HiddenValue)));

        node.SetParameters(new Dictionary<string, object?>
        {
            ["visible"] = 22,
            [nameof(BridgeProbeNode.HiddenValue)] = 11
        });

        Assert.Equal(22, node.VisibleValue);
        Assert.Equal(99, node.HiddenValue);

        var clrParameters = node.GetParametersByClrName();
        Assert.True(clrParameters.ContainsKey(nameof(BridgeProbeNode.VisibleValue)));
        Assert.False(clrParameters.ContainsKey(nameof(BridgeProbeNode.HiddenValue)));

        node.SetParameters(new Dictionary<string, object?>
        {
            [nameof(BridgeProbeNode.VisibleValue)] = 31
        });

        Assert.Equal(31, node.VisibleValue);
    }


    [Fact]
    public void RejectionChanges_ShouldRaiseLayoutInvalidatedEvent()
    {
        var node = new RejectionProbeNode();
        var count = 0;
        node.LayoutInvalidated += (_, _) => count++;

        node.Reject("a");
        node.Reject("a");
        node.Allow("a");

        Assert.Equal(2, count);
    }

    [Fact]
    public void MetadataProvider_ShouldPreferGeneratedRegistry_WhenAvailable()
    {
        BaseFramework.Core.Generated.GeneratedMetadataRegistry.Register(
            typeof(GeneratedNode),
            static () => new BaseFramework.Core.Metadata.InspectableTypeMetadata(
                typeof(GeneratedNode),
                new[]
                {
                    new BaseFramework.Core.Metadata.InspectableMemberMetadata(
                        "generated",
                        "Generated",
                        BaseFramework.Core.Metadata.MemberKind.String,
                        true,
                        0,
                        typeof(string),
                        null,
                        null,
                        null)
                }));

        var provider = new ReflectionObjectMetadataProvider();
        var metadata = provider.GetMetadata(typeof(GeneratedNode));

        Assert.Single(metadata.Members);
        Assert.Equal("generated", metadata.Members[0].Key);
    }

    [Fact]
    public void MetadataProvider_ShouldCacheAndOrderMembers()
    {
        var provider = new ReflectionObjectMetadataProvider();

        var first = provider.GetMetadata(typeof(TestNode));
        var second = provider.GetMetadata(typeof(TestNode));

        Assert.Same(first, second);
        Assert.Collection(first.Members,
            m => Assert.Equal("value", m.Key),
            m => Assert.Equal("second", m.Key),
            m => Assert.Equal("run", m.Key));
    }

    [Fact]
    public void MetadataProvider_ShouldCaptureValueSourceProperty()
    {
        var provider = new ReflectionObjectMetadataProvider();
        var metadata = provider.GetMetadata(typeof(ValueSourceNode));
        var selection = Assert.Single(metadata.Members, m => m.Key == "selection");
        Assert.NotNull(selection.ValueSourceProperty);
        Assert.Equal(nameof(ValueSourceNode.Options), selection.ValueSourceProperty!.Name);
    }

    [Fact]
    public void MetadataProvider_ShouldRecognizeNoteDocument()
    {
        var provider = new ReflectionObjectMetadataProvider();
        var metadata = provider.GetMetadata(typeof(NoteProbeNode));
        var note = Assert.Single(metadata.Members, m => m.Key == "note");
        Assert.Equal(BaseFramework.Core.Metadata.MemberKind.Note, note.Kind);
    }


    private sealed class RejectionProbeNode : ObservableObject
    {
        public void Reject(string key) => AddRejection(key);
        public void Allow(string key) => RemoveRejection(key);

        protected override void OnUpdate()
        {
        }
    }

    private sealed class GeneratedNode : ObservableObject
    {
        protected override void OnUpdate()
        {
        }
    }

    private sealed class TestNode : ObservableObject
    {
        public TestNode(UndoRedoManager? manager = null) : base(manager)
        {
        }

        [InspectableMember("value", "Value", Order = 1)]
        public int Value
        {
            get => Get<int>();
            set => Set(value);
        }

        [InspectableMember("second", "Secondary", Order = 2)]
        public int Secondary
        {
            get => Get<int>();
            set => Set(value);
        }

        [InspectableMember("run", "Run", Order = 3)]
        public void Run(int step)
        {
            Value += step;
        }

        protected override void OnUpdate()
        {
        }
    }

    private sealed class BridgeProbeNode : ParameterBridgeObject
    {
        [InspectableMember("visible", "Visible", Order = 1)]
        public int VisibleValue
        {
            get => Get<int>();
            set => Set(value);
        }

        public int HiddenValue
        {
            get => Get<int>();
            set => Set(value);
        }

        protected override void OnUpdate()
        {
        }
    }

    private sealed class ValueSourceNode : ObservableObject
    {
        public IEnumerable<string> Options => new[] { "One", "Two", "Three" };

        [InspectableMember("selection", "Selection", Order = 1, ValueSourcePropertyName = nameof(Options))]
        public string Selection
        {
            get => Get<string>() ?? Options.First();
            set => Set(value);
        }

        protected override void OnUpdate()
        {
        }
    }

    private sealed class NoteProbeNode : ObservableObject
    {
        [InspectableMember("note", "Note", Order = 1)]
        public NoteDocument Note
        {
            get => Get<NoteDocument>() ?? new NoteDocument();
            set => Set(value ?? new NoteDocument());
        }

        protected override void OnUpdate()
        {
        }
    }

    private sealed class UpdateProbeNode : ObservableObject
    {
        private static int _sequence;

        public int UpdateCount { get; private set; }
        public int LastUpdateOrder { get; private set; }

        public int Probe
        {
            get => Get<int>();
            set => Set(value);
        }

        public void Touch() => Set(Probe + 1, nameof(Probe));

        protected override void OnUpdate()
        {
            UpdateCount++;
            LastUpdateOrder = Interlocked.Increment(ref _sequence);
        }
    }
}
