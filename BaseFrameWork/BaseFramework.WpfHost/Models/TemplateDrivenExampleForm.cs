using BaseFramework.Core;
using BaseFramework.Core.Access;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.WpfHost.Models;

public sealed class TemplateDrivenExampleForm : ObservableObject, IRuntimeInspectableMetadataSource
{
    private readonly IReadOnlyList<TemplateFieldSpec> _fields;

    public TemplateDrivenExampleForm()
    {
        var order = 0;
        _fields =
        [
            new TemplateFieldSpec(++order, "template.project_name", "Project Name", MemberKind.String, typeof(string), "Template-Driven Form", "Autofill", "project.name", "project.name", null, null, "Relay Upgrade"),
            new TemplateFieldSpec(++order, "template.document_type", "Document Type", MemberKind.Selection, typeof(string), "Template-Driven Form", "Selection", "document.type", null, EditorHints.Selection, ["Acceptance Report", "Commissioning Note", "Factory Test Report"], "Acceptance Report"),
            new TemplateFieldSpec(++order, "template.test_date", "Test Date", MemberKind.DateTime, typeof(DateTime), "Template-Driven Form", "Autofill", "project.test_date", "project.test_date", null, null, DateTime.Today.AddDays(2)),
            new TemplateFieldSpec(++order, "template.summary", "Summary Note", MemberKind.MultiLineText, typeof(string), "Template-Driven Form", "User Input", "summary.note", null, EditorHints.Multiline, null, "Inspector metadata can be created at runtime from a discovered template."),
            new TemplateFieldSpec(++order, "template.setup_image", "Setup Image", MemberKind.Image, typeof(string), "Template-Driven Form", "Files", "image.setup", null, EditorHints.Image, null, @"C:\templates\images\setup.png"),
            new TemplateFieldSpec(++order, "template.results_table", "Results Table", MemberKind.Table, typeof(string), "Template-Driven Form", "Tables", "table.results", null, EditorHints.Table, null, "Voltage,Pass\r\nCurrent,Pass"),
            new TemplateFieldSpec(++order, "template.approval_note", "Approval Note", MemberKind.MultiLineText, typeof(string), "Template-Driven Form", "Restricted", "approval.note", null, EditorHints.Multiline, null, "Visible to generators, editable only with restricted permission.", VisiblePermissions: ["documents.generate"], EditablePermissions: ["fields.edit.restricted"])
        ];

        foreach (var field in _fields)
        {
            ApplyExternalValue(field.Key, field.InitialValue);
        }
    }

    public InspectableTypeMetadata GetRuntimeMetadata()
        => new(
            GetType(),
            _fields.Select(field => new InspectableMemberMetadata(
                field.Key,
                field.DisplayName,
                field.Kind,
                false,
                field.Order,
                field.ValueType,
                null,
                null)
            {
                ClrName = field.Key,
                Section = field.Section,
                Category = field.Category,
                EditorHint = field.EditorHint,
                PersistenceKey = field.PersistenceKey,
                DatabaseKey = field.DatabaseKey,
                AccessRules = new InspectableAccessRules(
                    field.VisibleRoles,
                    field.VisiblePermissions,
                    field.EditableRoles,
                    field.EditablePermissions,
                    Array.Empty<string>(),
                    Array.Empty<string>()),
                Getter = static (target, metadata) => ((TemplateDrivenExampleForm)target).GetRaw(metadata.Key),
                Setter = static (target, value, metadata) => ((TemplateDrivenExampleForm)target).ApplyExternalValue(metadata.Key, value),
                ValueSourceAccessor = field.AllowedValues.Count == 0
                    ? null
                    : static (target, metadata) => ((TemplateDrivenExampleForm)target).GetAllowedValues(metadata.Key)
            }).ToList());

    protected override void OnUpdate()
    {
    }

    private IEnumerable<string> GetAllowedValues(string key)
        => _fields.First(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)).AllowedValues;

    private sealed record TemplateFieldSpec(
        int Order,
        string Key,
        string DisplayName,
        MemberKind Kind,
        Type ValueType,
        string Section,
        string Category,
        string PersistenceKey,
        string? DatabaseKey,
        string? EditorHint,
        IReadOnlyList<string>? AllowedValues,
        object? InitialValue,
        IReadOnlyList<string>? VisibleRoles = null,
        IReadOnlyList<string>? VisiblePermissions = null,
        IReadOnlyList<string>? EditableRoles = null,
        IReadOnlyList<string>? EditablePermissions = null)
    {
        public IReadOnlyList<string> AllowedValues { get; init; } = AllowedValues ?? Array.Empty<string>();
        public IReadOnlyList<string> VisibleRoles { get; init; } = VisibleRoles ?? Array.Empty<string>();
        public IReadOnlyList<string> VisiblePermissions { get; init; } = VisiblePermissions ?? Array.Empty<string>();
        public IReadOnlyList<string> EditableRoles { get; init; } = EditableRoles ?? Array.Empty<string>();
        public IReadOnlyList<string> EditablePermissions { get; init; } = EditablePermissions ?? Array.Empty<string>();
    }
}
