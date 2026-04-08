using System;
using System.Collections;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.WpfHost.Controls;

public partial class ObjectInspectorControl : UserControl
{
    private ObservableObject? _target;
    private IObjectMetadataProvider? _provider;
    private bool _isRebuilding;
    private bool _pendingRebuild;

    public ObjectInspectorControl()
    {
        InitializeComponent();
    }

    public void Bind(ObservableObject target, IObjectMetadataProvider provider)
    {
        if (_target is not null)
        {
            _target.LayoutInvalidated -= HandleLayoutInvalidated;
        }

        _target = target;
        _provider = provider;
        _target.LayoutInvalidated += HandleLayoutInvalidated;
        RequestRebuild();
    }

    public void Clear()
    {
        if (_target is not null)
        {
            _target.LayoutInvalidated -= HandleLayoutInvalidated;
        }

        _target = null;
        _provider = null;
        _pendingRebuild = false;
        RootPanel.Children.Clear();
    }

    private void HandleLayoutInvalidated(object? sender, EventArgs e) => RequestRebuild();

    private void RequestRebuild()
    {
        if (_isRebuilding)
        {
            _pendingRebuild = true;
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        if (_target is null || _provider is null)
        {
            return;
        }

        try
        {
            _isRebuilding = true;
            RootPanel.Children.Clear();
            var metadata = _provider.GetMetadata(_target.GetType());

            var orderedMembers = metadata.Members
                .Where(m => !_target.IsRejected(m.Key))
                .OrderBy(GetGroupOrder)
                .ThenBy(m => m.Order)
                .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var member in orderedMembers)
            {
                var control = CreateControl(member, _target, _provider);
                if (control is not null)
                {
                    RootPanel.Children.Add(control);
                }
            }
        }
        finally
        {
            _isRebuilding = false;
            if (_pendingRebuild)
            {
                _pendingRebuild = false;
                Rebuild();
            }
        }
    }

    private static int GetGroupOrder(InspectableMemberMetadata member)
        => member.Kind switch
        {
            MemberKind.Method => 0,
            MemberKind.Collection => 2,
            _ => 1
        };

    private static FrameworkElement? CreateControl(InspectableMemberMetadata member, ObservableObject target, IObjectMetadataProvider provider)
        => member.Kind switch
        {
            MemberKind.Integer => new IntegerEditorControl(target, member),
            MemberKind.Double => new DoubleEditorControl(target, member),
            MemberKind.Enum => new EnumEditorControl(target, member),
            MemberKind.Boolean => new BooleanEditorControl(target, member),
            MemberKind.String => new StringEditorControl(target, member),
            MemberKind.Note => new NoteEditorControl(target, member),
            MemberKind.Class => member.Property?.GetValue(target) is ObservableObject child ? new ClassEditorControl(member.DisplayName, child, provider, target, member) : null,
            MemberKind.Collection => new CollectionEditorControl(target, member, provider),
            MemberKind.Method => new MethodEditorControl(target, member, provider),
            MemberKind.DateTime => new DateTimeEditorControl(target, member),
            _ => null
        };
}
