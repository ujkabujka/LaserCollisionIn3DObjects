using System.Reflection;
using BaseFramework.Core.Access;

namespace BaseFramework.Core.Metadata;

public sealed record InspectableMemberMetadata
{
    public InspectableMemberMetadata(
        string key,
        string displayName,
        MemberKind kind,
        bool readOnly,
        int order,
        Type valueType,
        PropertyInfo? property,
        MethodInfo? method,
        PropertyInfo? valueSourceProperty = null,
        IReadOnlyList<InspectableMemberMetadata>? parameters = null,
        object? defaultValue = null)
    {
        Key = key;
        ClrName = property?.Name ?? method?.Name ?? key;
        DisplayName = displayName;
        Kind = kind;
        ReadOnly = readOnly;
        Order = order;
        ValueType = valueType;
        Property = property;
        Method = method;
        ValueSourceProperty = valueSourceProperty;
        Parameters = parameters ?? Array.Empty<InspectableMemberMetadata>();
        DefaultValue = defaultValue;
        Getter = property is null ? null : static (target, metadata) => metadata.Property?.GetValue(target);
        Setter = property is null
            ? null
            : static (target, value, metadata) =>
            {
                if (metadata.Property is not null)
                {
                    metadata.Property.SetValue(target, value);
                }
            };
        Invoker = method is null
            ? null
            : static (target, parameters, metadata) => metadata.Method?.Invoke(target, parameters.ToArray());
        ValueSourceAccessor = valueSourceProperty is null
            ? null
            : static (target, metadata) => metadata.ValueSourceProperty?.GetValue(target) as System.Collections.IEnumerable;
    }

    public string Key { get; init; }
    public string ClrName { get; init; }
    public string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Section { get; init; }
    public string? HelpText { get; init; }
    public string? EditorHint { get; init; }
    public string? PersistenceKey { get; init; }
    public string? DatabaseKey { get; init; }
    public MemberKind Kind { get; init; }
    public bool ReadOnly { get; init; }
    public int Order { get; init; }
    public Type ValueType { get; init; }
    public PropertyInfo? Property { get; init; }
    public MethodInfo? Method { get; init; }
    public PropertyInfo? ValueSourceProperty { get; init; }
    public IReadOnlyList<InspectableMemberMetadata> Parameters { get; init; } = Array.Empty<InspectableMemberMetadata>();
    public object? DefaultValue { get; init; }
    public InspectableAccessRules AccessRules { get; init; } = InspectableAccessRules.Empty;
    public InspectableValidationHints ValidationHints { get; init; } = InspectableValidationHints.Empty;
    public MemberAccessEvaluation EffectiveAccess { get; init; } = MemberAccessEvaluation.Allowed;
    public Func<object, InspectableMemberMetadata, object?>? Getter { get; init; }
    public Action<object, object?, InspectableMemberMetadata>? Setter { get; init; }
    public Func<object, IReadOnlyList<object?>, InspectableMemberMetadata, object?>? Invoker { get; init; }
    public Func<object, InspectableMemberMetadata, System.Collections.IEnumerable?>? ValueSourceAccessor { get; init; }
}
