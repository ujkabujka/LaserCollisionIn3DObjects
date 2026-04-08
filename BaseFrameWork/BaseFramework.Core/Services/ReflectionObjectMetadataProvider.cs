using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using BaseFramework.Core.Access;
using BaseFramework.Core.Attributes;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Generated;

namespace BaseFramework.Core.Services;

public sealed class ReflectionObjectMetadataProvider : IObjectMetadataProvider
{
    private readonly ConcurrentDictionary<Type, InspectableTypeMetadata> _cache = new();

    public InspectableTypeMetadata GetMetadata(object target)
    {
        if (target is IRuntimeInspectableMetadataSource runtimeInspectable)
        {
            return runtimeInspectable.GetRuntimeMetadata();
        }

        return GetMetadata(target.GetType());
    }

    public InspectableTypeMetadata GetMetadata(Type targetType)
    {
        if (GeneratedMetadataRegistry.TryCreate(targetType, out var generated) && generated.Members.Count > 0)
        {
            return generated;
        }

        return _cache.GetOrAdd(targetType, BuildMetadata);
    }

    private static InspectableTypeMetadata BuildMetadata(Type targetType)
    {
        var list = new List<InspectableMemberMetadata>();

        foreach (var property in targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attr = property.GetCustomAttribute<InspectableMemberAttribute>();
            if (attr is null)
            {
                continue;
            }

            var presentation = property.GetCustomAttribute<InspectablePresentationAttribute>();
            var editor = property.GetCustomAttribute<InspectableEditorAttribute>();
            var persistence = property.GetCustomAttribute<InspectablePersistenceAttribute>();
            var access = property.GetCustomAttribute<InspectableAccessAttribute>();
            var validation = property.GetCustomAttribute<InspectableValidationAttribute>();
            var valueSource = ResolveValueSourceProperty(targetType, attr.ValueSourcePropertyName);

            list.Add(new InspectableMemberMetadata(
                attr.Key,
                attr.DisplayName,
                MemberKindResolver.Resolve(property.PropertyType, editor?.Hint, valueSource is not null),
                attr.ReadOnly || !property.CanWrite,
                attr.Order,
                property.PropertyType,
                property,
                null,
                valueSource)
            {
                ClrName = property.Name,
                Description = presentation?.Description,
                Category = presentation?.Category,
                Section = presentation?.Section,
                HelpText = presentation?.HelpText,
                EditorHint = editor?.Hint,
                PersistenceKey = persistence?.PersistenceKey,
                DatabaseKey = persistence?.DatabaseKey,
                AccessRules = BuildAccessRules(access),
                ValidationHints = BuildValidationHints(validation),
                Getter = static (target, metadata) => metadata.Property?.GetValue(target),
                Setter = static (target, value, metadata) =>
                {
                    if (metadata.Property is not null && metadata.Property.CanWrite)
                    {
                        metadata.Property.SetValue(target, value);
                    }
                },
                ValueSourceAccessor = valueSource is null
                    ? null
                    : static (target, metadata) => metadata.ValueSourceProperty?.GetValue(target) as IEnumerable
            });
        }

        foreach (var method in targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            var attr = method.GetCustomAttribute<InspectableMemberAttribute>();
            if (attr is null)
            {
                continue;
            }

            var parameters = method.GetParameters()
                .Select(p => new InspectableMemberMetadata(
                    p.Name ?? p.ParameterType.Name,
                    p.Name ?? p.ParameterType.Name,
                    MemberKindResolver.Resolve(p.ParameterType),
                    false,
                    0,
                    p.ParameterType,
                    null,
                    null,
                    null,
                    null,
                    p.HasDefaultValue ? p.DefaultValue : GetDefaultValue(p.ParameterType))
                {
                    ClrName = p.Name ?? p.ParameterType.Name,
                    DefaultValue = p.HasDefaultValue ? p.DefaultValue : GetDefaultValue(p.ParameterType)
                })
                .ToList();

            var presentation = method.GetCustomAttribute<InspectablePresentationAttribute>();
            var editor = method.GetCustomAttribute<InspectableEditorAttribute>();
            var persistence = method.GetCustomAttribute<InspectablePersistenceAttribute>();
            var access = method.GetCustomAttribute<InspectableAccessAttribute>();
            var validation = method.GetCustomAttribute<InspectableValidationAttribute>();

            list.Add(new InspectableMemberMetadata(
                attr.Key,
                attr.DisplayName,
                MemberKind.Method,
                true,
                attr.Order,
                method.ReturnType,
                null,
                method,
                null,
                parameters)
            {
                ClrName = method.Name,
                Description = presentation?.Description,
                Category = presentation?.Category,
                Section = presentation?.Section,
                HelpText = presentation?.HelpText,
                EditorHint = editor?.Hint,
                PersistenceKey = persistence?.PersistenceKey,
                DatabaseKey = persistence?.DatabaseKey,
                AccessRules = BuildAccessRules(access),
                ValidationHints = BuildValidationHints(validation),
                Invoker = static (target, parameterValues, metadata) => metadata.Method?.Invoke(target, parameterValues.ToArray())
            });
        }

        var ordered = list.OrderBy(m => m.Order).ThenBy(m => m.DisplayName).ToList();
        return new InspectableTypeMetadata(targetType, ordered);
    }

    private static object? GetDefaultValue(Type type)
    {
        if (!type.IsValueType) return null;
        return Activator.CreateInstance(type);
    }

    private static InspectableAccessRules BuildAccessRules(InspectableAccessAttribute? access)
    {
        if (access is null)
        {
            return InspectableAccessRules.Empty;
        }

        return new InspectableAccessRules(
            access.VisibleRoles,
            access.VisiblePermissions,
            access.EditableRoles,
            access.EditablePermissions,
            access.InvokeRoles,
            access.InvokePermissions);
    }

    private static InspectableValidationHints BuildValidationHints(InspectableValidationAttribute? validation)
    {
        if (validation is null)
        {
            return InspectableValidationHints.Empty;
        }

        return new InspectableValidationHints(
            validation.Required,
            double.IsNaN(validation.Minimum) ? null : validation.Minimum,
            double.IsNaN(validation.Maximum) ? null : validation.Maximum,
            validation.RegexPattern);
    }

    private static PropertyInfo? ResolveValueSourceProperty(Type targetType, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        var property = targetType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null)
        {
            return null;
        }

        if (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
        {
            return null;
        }

        return property;
    }
}
