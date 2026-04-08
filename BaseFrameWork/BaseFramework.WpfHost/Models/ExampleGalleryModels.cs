using System.Collections.ObjectModel;
using BaseFramework.Core.Api;
using BaseFramework.Core.Attributes;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Notes;

namespace BaseFramework.WpfHost.Models;

public enum WorkflowStepState
{
    Planned,
    Ready,
    Complete
}

[GenerateInspectorMetadata]
public sealed class BasicDocumentExampleModel : ParameterBridgeObject
{
    private static readonly IReadOnlyList<string> DocumentTypes =
    [
        "Acceptance Report",
        "Factory Test Report",
        "Commissioning Note"
    ];

    public BasicDocumentExampleModel()
    {
        ProjectCode = "ENG-001";
        DocumentType = DocumentTypes[0];
        TestFactor = 72.5;
        Summary = "This sample shows scalar fields, a value source, multiline text, method invocation, and a computed read-only field.";
        Notes = new NoteDocument("Stable keys make template and database mapping safer than relying on CLR member names.");
        RecalculateScore();
    }

    public IEnumerable<string> DocumentTypeOptions => DocumentTypes;

    [InspectableMember("example.basic.project_code", "Project Code", Order = 1)]
    [InspectablePresentation(Section = "1. Scalar Fields", Category = "Basics", Description = "Stable business key for later template and persistence mapping.")]
    [InspectableValidation(Required = true, RegexPattern = "^[A-Z]{3}-\\d{3}$")]
    public string ProjectCode
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("example.basic.document_type", "Document Type", Order = 2, ValueSourcePropertyName = nameof(DocumentTypeOptions))]
    [InspectablePresentation(Section = "2. Dropdown Source", Category = "Selection", Description = "Value-source driven dropdown example.")]
    public string DocumentType
    {
        get => Get<string>() ?? DocumentTypes[0];
        set => Set(value);
    }

    [InspectableMember("example.basic.test_factor", "Test Factor", Order = 3)]
    [InspectablePresentation(Section = "1. Scalar Fields", Category = "Numbers", HelpText = "Validation hints are attached to the metadata even if this demo does not render them yet.")]
    [InspectableValidation(Minimum = 0, Maximum = 100)]
    public double TestFactor
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("example.basic.summary", "Executive Summary", Order = 4)]
    [InspectableEditor(EditorHints.Multiline)]
    [InspectablePresentation(Section = "3. Text Inputs", Category = "Multiline", Description = "String field rendered with a multiline editor hint.")]
    public string Summary
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("example.basic.notes", "Engineer Notes", Order = 5)]
    [InspectablePresentation(Section = "3. Text Inputs", Category = "Rich Note", Description = "NoteDocument exercises the note editor.")]
    public NoteDocument Notes
    {
        get => Get<NoteDocument>() ?? new NoteDocument();
        set => Set(value ?? new NoteDocument());
    }

    [InspectableMember("example.basic.readiness", "Readiness Score", ReadOnly = true, Order = 6)]
    [InspectablePresentation(Section = "4. Computed Field", Category = "Read-Only", Description = "Read-only field derived from other values.")]
    public double ReadinessScore
    {
        get => Get<double>();
        private set => Set(value);
    }

    [InspectableMember("example.basic.recalculate", "Recalculate Score", Order = 7)]
    [InspectablePresentation(Section = "5. Method Invocation", Category = "Commands", Description = "Invokes logic without opening a handwritten screen.")]
    public void RecalculateScore()
    {
        var typeWeight = string.Equals(DocumentType, DocumentTypes[0], StringComparison.OrdinalIgnoreCase) ? 10d : 4d;
        ReadinessScore = Math.Round(Math.Clamp(TestFactor + typeWeight, 0d, 100d), 2);
    }

    protected override void OnUpdate()
    {
        RecalculateScore();
    }
}

[GenerateInspectorMetadata]
public sealed class NestedWorkflowExampleModel : ParameterBridgeObject
{
    public NestedWorkflowExampleModel()
    {
        ApprovalSettings = new ApprovalSettingsExampleModel();
        AddDependency(ApprovalSettings);

        WorkflowSteps = new ObservableCollection<WorkflowStepExample>
        {
            new() { StepName = "Visual inspection", Owner = "Elif", DueDate = DateTime.Today.AddDays(1), State = WorkflowStepState.Ready },
            new() { StepName = "Power-on test", Owner = "Mert", DueDate = DateTime.Today.AddDays(2), State = WorkflowStepState.Planned }
        };

        foreach (var step in WorkflowSteps)
        {
            AddDependency(step);
        }

        ShowApprovalSettings = true;
        RefreshOverview();
    }

    [InspectableMember("example.nested.show_approval", "Show Approval Settings", Order = 1)]
    [InspectablePresentation(Section = "6. Conditional Layout", Category = "Visibility", Description = "Toggles rejection-based visibility for the nested object below.")]
    public bool ShowApprovalSettings
    {
        get => Get<bool>();
        set
        {
            Set(value);
            if (value)
            {
                RemoveRejection("example.nested.approval_settings");
            }
            else
            {
                AddRejection("example.nested.approval_settings");
            }
        }
    }

    [InspectableMember("example.nested.approval_settings", "Approval Settings", Order = 2)]
    [InspectablePresentation(Section = "7. Nested Object", Category = "Object Graph", Description = "Nested inspector rendering reuses the same metadata and editor pipeline.")]
    public ApprovalSettingsExampleModel ApprovalSettings
    {
        get => Get<ApprovalSettingsExampleModel>() ?? new ApprovalSettingsExampleModel();
        set
        {
            Set(value);
            AddDependency(value);
        }
    }

    [InspectableMember("example.nested.workflow_steps", "Workflow Steps", Order = 3)]
    [InspectablePresentation(Section = "8. Collection", Category = "Collection Editor", Description = "ObservableCollection of child objects.")]
    public ObservableCollection<WorkflowStepExample> WorkflowSteps
    {
        get => Get<ObservableCollection<WorkflowStepExample>>() ?? new ObservableCollection<WorkflowStepExample>();
        set => Set(value);
    }

    [InspectableMember("example.nested.add_step", "Add Example Step", Order = 4)]
    [InspectablePresentation(Section = "8. Collection", Category = "Collection Editor", Description = "Method that appends a new child item.")]
    public void AddStep()
    {
        var nextIndex = WorkflowSteps.Count + 1;
        var step = new WorkflowStepExample
        {
            StepName = $"Generated Step {nextIndex}",
            Owner = nextIndex % 2 == 0 ? "Aylin" : "Deniz",
            DueDate = DateTime.Today.AddDays(nextIndex),
            State = WorkflowStepState.Planned
        };

        WorkflowSteps.Add(step);
        AddDependency(step);
        RefreshOverview();
    }

    [InspectableMember("example.nested.overview", "Workflow Overview", ReadOnly = true, Order = 5)]
    [InspectablePresentation(Section = "9. Computed Summary", Category = "Read-Only", Description = "Summarises nested and collection state.")]
    public string Overview
    {
        get => Get<string>() ?? string.Empty;
        private set => Set(value);
    }

    protected override void OnUpdate()
    {
        RefreshOverview();
    }

    private void RefreshOverview()
    {
        Overview = $"{WorkflowSteps.Count} workflow steps, {ApprovalSettings.RequiredApprovers} required approvers, final sign-off {(ApprovalSettings.RequiresFinalSignoff ? "enabled" : "disabled")}.";
    }
}

[GenerateInspectorMetadata]
public sealed class ApprovalSettingsExampleModel : ParameterBridgeObject
{
    public ApprovalSettingsExampleModel()
    {
        RequiredApprovers = 2;
        RequiresFinalSignoff = true;
        ApprovalComment = "Final sign-off is required for customer-facing document releases.";
    }

    [InspectableMember("example.approval.required_approvers", "Required Approvers", Order = 1)]
    [InspectablePresentation(Section = "Approval", Category = "Basics")]
    [InspectableValidation(Minimum = 1, Maximum = 5)]
    public int RequiredApprovers
    {
        get => Get<int>();
        set => Set(value);
    }

    [InspectableMember("example.approval.final_signoff", "Requires Final Sign-Off", Order = 2)]
    [InspectablePresentation(Section = "Approval", Category = "Flags")]
    public bool RequiresFinalSignoff
    {
        get => Get<bool>();
        set => Set(value);
    }

    [InspectableMember("example.approval.comment", "Approval Comment", Order = 3)]
    [InspectableEditor(EditorHints.Multiline)]
    [InspectablePresentation(Section = "Approval", Category = "Notes")]
    public string ApprovalComment
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    protected override void OnUpdate()
    {
    }
}

[GenerateInspectorMetadata]
public sealed class WorkflowStepExample : ParameterBridgeObject
{
    public WorkflowStepExample()
    {
        StepName = "New Step";
        Owner = "Engineer";
        DueDate = DateTime.Today;
        State = WorkflowStepState.Planned;
    }

    [InspectableMember("example.step.name", "Step Name", Order = 1)]
    [InspectablePresentation(Section = "Step", Category = "Basics")]
    public string StepName
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("example.step.owner", "Owner", Order = 2)]
    [InspectablePresentation(Section = "Step", Category = "Basics")]
    public string Owner
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("example.step.due", "Due Date", Order = 3)]
    [InspectablePresentation(Section = "Step", Category = "Schedule")]
    public DateTime DueDate
    {
        get => Get<DateTime>();
        set => Set(value);
    }

    [InspectableMember("example.step.state", "State", Order = 4)]
    [InspectablePresentation(Section = "Step", Category = "Status")]
    public WorkflowStepState State
    {
        get => Get<WorkflowStepState>();
        set => Set(value);
    }

    protected override void OnUpdate()
    {
    }
}

[GenerateInspectorMetadata]
public sealed class RoleAwareExampleModel : ParameterBridgeObject
{
    public RoleAwareExampleModel()
    {
        PublicComment = "Everyone can see this field.";
        DesignerNotes = "Visible to Designer and Admin.";
        RestrictedApprovalNote = "Editing this note requires the restricted field permission.";
        LastAction = "No action invoked yet.";
    }

    [InspectableMember("example.role.public_comment", "Public Comment", Order = 1)]
    [InspectablePresentation(Section = "Visible To Everyone", Category = "Open Fields")]
    public string PublicComment
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("example.role.designer_notes", "Designer Notes", Order = 2)]
    [InspectablePresentation(Section = "Role-Based Visibility", Category = "Role Rules", Description = "Only Designer and Admin should see this field.")]
    [InspectableAccess(VisibleRoles = ["Designer", "Admin"], EditableRoles = ["Designer", "Admin"])]
    public string DesignerNotes
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("example.role.restricted_approval", "Restricted Approval Note", Order = 3)]
    [InspectablePresentation(Section = "Permission-Based Editing", Category = "Permission Rules", Description = "Visible to generators, editable only to restricted editors.")]
    [InspectableAccess(VisiblePermissions = ["documents.generate"], EditablePermissions = ["fields.edit.restricted"])]
    public string RestrictedApprovalNote
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("example.role.generate", "Generate Preview", Order = 4)]
    [InspectablePresentation(Section = "Action Permissions", Category = "Commands", Description = "Invoke access is permission-driven too.")]
    [InspectableAccess(InvokePermissions = ["documents.generate"])]
    public void GeneratePreview()
    {
        LastAction = $"Preview generated at {DateTime.Now:HH:mm:ss}.";
    }

    [InspectableMember("example.role.last_action", "Last Action", ReadOnly = true, Order = 5)]
    [InspectablePresentation(Section = "Action Permissions", Category = "Read-Only")]
    public string LastAction
    {
        get => Get<string>() ?? string.Empty;
        private set => Set(value);
    }

    protected override void OnUpdate()
    {
    }
}
