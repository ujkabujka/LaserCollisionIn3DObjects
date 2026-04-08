using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BaseFramework.Generators;

[Generator]
public sealed class InspectorMetadataGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                transform: static (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null)
            .Select(static (symbol, _) => symbol!);

        context.RegisterSourceOutput(types, static (spc, typeSymbol) =>
        {
            var hasAttr = typeSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == "GenerateInspectorMetadataAttribute");
            if (!hasAttr)
            {
                return;
            }

            var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace ? "" : $"namespace {typeSymbol.ContainingNamespace.ToDisplayString()};";
            var fullName = typeSymbol.ToDisplayString();
            var members = BuildMembers(typeSymbol);
            var source = new StringBuilder();
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using System.Linq;");
            source.AppendLine("using BaseFramework.Core.Generated;");
            source.AppendLine("using BaseFramework.Core.Metadata;");
            source.AppendLine("using BaseFramework.Core.Access;");
            if (!string.IsNullOrWhiteSpace(ns)) source.AppendLine(ns);
            source.AppendLine($"internal static class {typeSymbol.Name}GeneratedMetadataRegistration");
            source.AppendLine("{");
            source.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
            source.AppendLine("    internal static void Register()");
            source.AppendLine("    {");
            source.AppendLine($"        GeneratedMetadataRegistry.Register(typeof({fullName}), Create);");
            source.AppendLine("    }");
            source.AppendLine();
            source.AppendLine($"    private static InspectableTypeMetadata Create()");
            source.AppendLine("    {");
            source.AppendLine($"        return new InspectableTypeMetadata(typeof({fullName}), new List<InspectableMemberMetadata>");
            source.AppendLine("        {");
            foreach (var member in members)
            {
                source.AppendLine(member);
            }
            source.AppendLine("        });");
            source.AppendLine("    }");
            source.AppendLine("}");

            spc.AddSource($"{typeSymbol.Name}.InspectorMetadata.g.cs", source.ToString());
        });
    }

    private static IReadOnlyList<string> BuildMembers(INamedTypeSymbol typeSymbol)
    {
        var members = new List<string>();

        foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var inspectable = GetAttribute(property, "InspectableMemberAttribute");
            if (inspectable is null)
            {
                continue;
            }

            members.Add(BuildPropertyMetadata(typeSymbol, property, inspectable));
        }

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.IsStatic || method.MethodKind != MethodKind.Ordinary || method.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var inspectable = GetAttribute(method, "InspectableMemberAttribute");
            if (inspectable is null)
            {
                continue;
            }

            members.Add(BuildMethodMetadata(typeSymbol, method, inspectable));
        }

        return members;
    }

    private static string BuildPropertyMetadata(INamedTypeSymbol owner, IPropertySymbol property, AttributeData inspectable)
    {
        var fullOwner = owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var key = GetConstructorArgument(inspectable, 0) ?? property.Name;
        var displayName = GetConstructorArgument(inspectable, 1) ?? property.Name;
        var readOnly = GetNamedArgument(inspectable, "ReadOnly", "false");
        var order = GetNamedArgument(inspectable, "Order", "0");
        var valueSourcePropertyName = GetNamedArgument(inspectable, "ValueSourcePropertyName", "null");
        var editorAttribute = GetAttribute(property, "InspectableEditorAttribute");
        var editorHint = GetConstructorArgument(editorAttribute, 0) ?? "null";
        var presentationAttribute = GetAttribute(property, "InspectablePresentationAttribute");
        var persistenceAttribute = GetAttribute(property, "InspectablePersistenceAttribute");
        var accessAttribute = GetAttribute(property, "InspectableAccessAttribute");
        var validationAttribute = GetAttribute(property, "InspectableValidationAttribute");

        var hasValueSource = valueSourcePropertyName != "null";

        return $$"""
            new InspectableMemberMetadata({{key}}, {{displayName}}, MemberKindResolver.Resolve(typeof({{propertyType}}), {{editorHint}}, {{(hasValueSource ? "true" : "false")}}), {{readOnly}} || {{(property.SetMethod is null || property.SetMethod.DeclaredAccessibility != Accessibility.Public ? "true" : "false")}}, {{order}}, typeof({{propertyType}}), null, null)
            {
                ClrName = "{{property.Name}}",
                Description = {{GetNamedArgument(presentationAttribute, "Description", "null")}},
                Category = {{GetNamedArgument(presentationAttribute, "Category", "null")}},
                Section = {{GetNamedArgument(presentationAttribute, "Section", "null")}},
                HelpText = {{GetNamedArgument(presentationAttribute, "HelpText", "null")}},
                EditorHint = {{editorHint}},
                PersistenceKey = {{GetConstructorArgument(persistenceAttribute, 0) ?? "null"}},
                DatabaseKey = {{GetNamedArgument(persistenceAttribute, "DatabaseKey", "null")}},
                AccessRules = {{BuildAccessRules(accessAttribute)}},
                ValidationHints = {{BuildValidationHints(validationAttribute)}},
                Getter = static (target, metadata) => (({{fullOwner}})target).{{property.Name}},
                Setter = {{BuildPropertySetter(property, fullOwner)}},
                ValueSourceAccessor = {{BuildValueSourceAccessor(property, fullOwner, valueSourcePropertyName)}}
            },
            """;
    }

    private static string BuildMethodMetadata(INamedTypeSymbol owner, IMethodSymbol method, AttributeData inspectable)
    {
        var fullOwner = owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var key = GetConstructorArgument(inspectable, 0) ?? method.Name;
        var displayName = GetConstructorArgument(inspectable, 1) ?? method.Name;
        var order = GetNamedArgument(inspectable, "Order", "0");
        var editorAttribute = GetAttribute(method, "InspectableEditorAttribute");
        var presentationAttribute = GetAttribute(method, "InspectablePresentationAttribute");
        var persistenceAttribute = GetAttribute(method, "InspectablePersistenceAttribute");
        var accessAttribute = GetAttribute(method, "InspectableAccessAttribute");
        var validationAttribute = GetAttribute(method, "InspectableValidationAttribute");
        var parameters = string.Join("\n", method.Parameters.Select(BuildParameterMetadata));
        var invokeArgs = string.Join(", ", method.Parameters.Select((parameter, index) => $"({parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})parameters[{index}]!"));

        return $$"""
            new InspectableMemberMetadata({{key}}, {{displayName}}, MemberKind.Method, true, {{order}}, typeof({{returnType}}), null, null, null, new List<InspectableMemberMetadata>
            {
            {{Indent(parameters, 4)}}
            })
            {
                ClrName = "{{method.Name}}",
                Description = {{GetNamedArgument(presentationAttribute, "Description", "null")}},
                Category = {{GetNamedArgument(presentationAttribute, "Category", "null")}},
                Section = {{GetNamedArgument(presentationAttribute, "Section", "null")}},
                HelpText = {{GetNamedArgument(presentationAttribute, "HelpText", "null")}},
                EditorHint = {{GetConstructorArgument(editorAttribute, 0) ?? "null"}},
                PersistenceKey = {{GetConstructorArgument(persistenceAttribute, 0) ?? "null"}},
                DatabaseKey = {{GetNamedArgument(persistenceAttribute, "DatabaseKey", "null")}},
                AccessRules = {{BuildAccessRules(accessAttribute)}},
                ValidationHints = {{BuildValidationHints(validationAttribute)}},
                Invoker = static (target, parameters, metadata) =>
                {
                    (({{fullOwner}})target).{{method.Name}}({{invokeArgs}});
                    return null;
                }
            },
            """;
    }

    private static string BuildParameterMetadata(IParameterSymbol parameter)
    {
        var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var defaultValue = parameter.HasExplicitDefaultValue
            ? FormatTypedValue(parameter.ExplicitDefaultValue)
            : "null";

        return $$"""
            new InspectableMemberMetadata("{{parameter.Name}}", "{{parameter.Name}}", MemberKindResolver.Resolve(typeof({{parameterType}})), false, 0, typeof({{parameterType}}), null, null, null, null, {{defaultValue}})
            {
                ClrName = "{{parameter.Name}}",
                DefaultValue = {{defaultValue}}
            },
            """;
    }

    private static string BuildPropertySetter(IPropertySymbol property, string fullOwner)
    {
        if (property.SetMethod is null || property.SetMethod.DeclaredAccessibility != Accessibility.Public)
        {
            return "null";
        }

        var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"static (target, value, metadata) => (({fullOwner})target).{property.Name} = ({propertyType})value!";
    }

    private static string BuildValueSourceAccessor(IPropertySymbol property, string fullOwner, string valueSourcePropertyName)
    {
        if (valueSourcePropertyName == "null")
        {
            return "null";
        }

        var trimmedName = valueSourcePropertyName.Trim('"');
        return $"static (target, metadata) => (({fullOwner})target).{trimmedName} as System.Collections.IEnumerable";
    }

    private static string BuildAccessRules(AttributeData? attribute)
    {
        if (attribute is null)
        {
            return "InspectableAccessRules.Empty";
        }

        return $$"""new InspectableAccessRules({{BuildStringArray(attribute, "VisibleRoles")}}, {{BuildStringArray(attribute, "VisiblePermissions")}}, {{BuildStringArray(attribute, "EditableRoles")}}, {{BuildStringArray(attribute, "EditablePermissions")}}, {{BuildStringArray(attribute, "InvokeRoles")}}, {{BuildStringArray(attribute, "InvokePermissions")}})""";
    }

    private static string BuildValidationHints(AttributeData? attribute)
    {
        if (attribute is null)
        {
            return "InspectableValidationHints.Empty";
        }

        var required = GetNamedArgument(attribute, "Required", "false");
        var minimum = GetNamedArgument(attribute, "Minimum", "double.NaN");
        var maximum = GetNamedArgument(attribute, "Maximum", "double.NaN");
        var regex = GetNamedArgument(attribute, "RegexPattern", "null");

        return $$"""new InspectableValidationHints({{required}}, double.IsNaN({{minimum}}) ? null : {{minimum}}, double.IsNaN({{maximum}}) ? null : {{maximum}}, {{regex}})""";
    }

    private static string BuildStringArray(AttributeData attribute, string name)
    {
        var named = attribute.NamedArguments.FirstOrDefault(item => item.Key == name);
        if (named.Key is null || named.Value.Kind != TypedConstantKind.Array || named.Value.Values.IsDefaultOrEmpty)
        {
            return "new string[0]";
        }

        var values = string.Join(", ", named.Value.Values.Select(value => FormatTypedValue(value.Value)));
        return $"new[] {{ {values} }}";
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string shortName)
        => symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name == shortName);

    private static string? GetConstructorArgument(AttributeData? attribute, int index)
    {
        if (attribute is null || attribute.ConstructorArguments.Length <= index)
        {
            return null;
        }

        return FormatTypedValue(attribute.ConstructorArguments[index].Value);
    }

    private static string GetNamedArgument(AttributeData? attribute, string name, string fallback)
    {
        if (attribute is null)
        {
            return fallback;
        }

        var match = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
        if (match.Key is null)
        {
            return fallback;
        }

        return FormatTypedValue(match.Value.Value);
    }

    private static string FormatTypedValue(object? value)
        => value switch
        {
            null => "null",
            bool boolean => boolean ? "true" : "false",
            string text => QuoteString(text),
            char character => QuoteChar(character),
            float single => single.ToString(System.Globalization.CultureInfo.InvariantCulture) + "F",
            double number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal decimalNumber => decimalNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + "M",
            long longNumber => longNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
            int integer => integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
            short shortNumber => shortNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            byte byteNumber => byteNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null"
        };

    private static string Indent(string text, int indent)
    {
        var padding = new string(' ', indent * 3);
        var lines = text.Split('\n');
        return string.Join("\n", lines.Select(line => string.IsNullOrWhiteSpace(line) ? line : padding + line));
    }

    private static string QuoteString(string value)
        => "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t") + "\"";

    private static string QuoteChar(char value)
        => value switch
        {
            '\'' => "'\\''",
            '\\' => "'\\\\'",
            '\r' => "'\\r'",
            '\n' => "'\\n'",
            '\t' => "'\\t'",
            _ => $"'{value}'"
        };
}
