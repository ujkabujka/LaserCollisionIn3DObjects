using BaseFramework.Core;
using BaseFramework.Core.Access;
using BaseFramework.Core.Api;
using BaseFramework.Core.Attributes;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.Core.Tests;

public sealed class InspectableMetadataTests
{
    [Fact]
    public void DefaultMemberAccessEvaluator_ShouldRespectVisibilityAndEditabilityRules()
    {
        var provider = new ReflectionObjectMetadataProvider();
        var metadata = provider.GetMetadata(typeof(AccessProbeNode));
        var restricted = Assert.Single(metadata.Members, member => member.Key == "restricted.field");
        var action = Assert.Single(metadata.Members, member => member.Key == "restricted.action");
        var evaluator = new DefaultMemberAccessEvaluator();

        var engineerContext = InspectableAccessContext.Create(
            "engineer",
            [ "SystemEngineer" ],
            [ "documents.generate" ]);
        var adminContext = InspectableAccessContext.Create(
            "admin",
            [ "Admin" ],
            [ "documents.generate", "fields.edit.restricted" ]);

        var engineerFieldAccess = evaluator.Evaluate(restricted, new AccessProbeNode(), engineerContext);
        var adminFieldAccess = evaluator.Evaluate(restricted, new AccessProbeNode(), adminContext);
        var engineerActionAccess = evaluator.Evaluate(action, new AccessProbeNode(), engineerContext);
        var adminActionAccess = evaluator.Evaluate(action, new AccessProbeNode(), adminContext);

        Assert.True(engineerFieldAccess.CanView);
        Assert.False(engineerFieldAccess.CanEdit);
        Assert.True(adminFieldAccess.CanView);
        Assert.True(adminFieldAccess.CanEdit);

        Assert.False(engineerActionAccess.CanInvoke);
        Assert.True(adminActionAccess.CanInvoke);
    }

    [Fact]
    public void GeneratedMetadata_ShouldMatchReflectionMetadata_ForEquivalentTypes()
    {
        var provider = new ReflectionObjectMetadataProvider();

        var generatedMetadata = provider.GetMetadata(typeof(GeneratedParityNode));
        var reflectionMetadata = provider.GetMetadata(typeof(ReflectionParityNode));

        Assert.Equal(reflectionMetadata.Members.Count, generatedMetadata.Members.Count);

        var generatedField = Assert.Single(generatedMetadata.Members, member => member.Key == "document.title");
        var reflectionField = Assert.Single(reflectionMetadata.Members, member => member.Key == "document.title");
        AssertEquivalent(generatedField, reflectionField);

        var generatedAction = Assert.Single(generatedMetadata.Members, member => member.Key == "document.publish");
        var reflectionAction = Assert.Single(reflectionMetadata.Members, member => member.Key == "document.publish");
        AssertEquivalent(generatedAction, reflectionAction);
        Assert.Single(generatedAction.Parameters);
        Assert.Equal("version", generatedAction.Parameters[0].ClrName);
        Assert.Equal(1, generatedAction.Parameters[0].DefaultValue);
    }

    private static void AssertEquivalent(InspectableMemberMetadata generated, InspectableMemberMetadata reflected)
    {
        Assert.Equal(reflected.Key, generated.Key);
        Assert.Equal(reflected.ClrName, generated.ClrName);
        Assert.Equal(reflected.DisplayName, generated.DisplayName);
        Assert.Equal(reflected.Description, generated.Description);
        Assert.Equal(reflected.Category, generated.Category);
        Assert.Equal(reflected.Section, generated.Section);
        Assert.Equal(reflected.EditorHint, generated.EditorHint);
        Assert.Equal(reflected.PersistenceKey, generated.PersistenceKey);
        Assert.Equal(reflected.DatabaseKey, generated.DatabaseKey);
        Assert.Equal(reflected.Kind, generated.Kind);
        Assert.Equal(reflected.ReadOnly, generated.ReadOnly);
        Assert.Equal(reflected.Order, generated.Order);
        Assert.Equal(reflected.ValueType, generated.ValueType);
        Assert.Equal(reflected.ValidationHints, generated.ValidationHints);
        Assert.Equal(reflected.AccessRules.VisibleRoles, generated.AccessRules.VisibleRoles);
        Assert.Equal(reflected.AccessRules.VisiblePermissions, generated.AccessRules.VisiblePermissions);
        Assert.Equal(reflected.AccessRules.EditablePermissions, generated.AccessRules.EditablePermissions);
        Assert.Equal(reflected.AccessRules.InvokePermissions, generated.AccessRules.InvokePermissions);
    }

    private sealed class AccessProbeNode : ObservableObject
    {
        [InspectableMember("restricted.field", "Restricted Field", Order = 1)]
        [InspectableAccess(VisiblePermissions = ["documents.generate"], EditablePermissions = ["fields.edit.restricted"])]
        public string RestrictedField
        {
            get => Get<string>() ?? string.Empty;
            set => Set(value);
        }

        [InspectableMember("restricted.action", "Restricted Action", Order = 2)]
        [InspectableAccess(InvokePermissions = ["fields.edit.restricted"])]
        public void RestrictedAction()
        {
        }

        protected override void OnUpdate()
        {
        }
    }

    [GenerateInspectorMetadata]
    public sealed class GeneratedParityNode : ParameterBridgeObject
    {
        [InspectableMember("document.title", "Document Title", Order = 1)]
        [InspectablePresentation(Description = "Primary title", Section = "Document", Category = "Identity", HelpText = "Shown on the cover page.")]
        [InspectableEditor(EditorHints.Multiline)]
        [InspectablePersistence("document.title", DatabaseKey = "project.title")]
        [InspectableAccess(VisibleRoles = ["Designer"], EditablePermissions = ["templates.manage"])]
        [InspectableValidation(Required = true, Minimum = 3, Maximum = 120, RegexPattern = "^[A-Za-z ]+$")]
        public string Title
        {
            get => Get<string>() ?? string.Empty;
            set => Set(value);
        }

        [InspectableMember("document.publish", "Publish", Order = 2)]
        [InspectablePresentation(Description = "Publishes the template", Section = "Actions", Category = "Commands")]
        [InspectableAccess(InvokePermissions = ["templates.manage"])]
        public void Publish(int version = 1)
        {
            ApplyExternalValue(nameof(Title), $"{Title} v{version}");
        }

        protected override void OnUpdate()
        {
        }
    }

    public sealed class ReflectionParityNode : ParameterBridgeObject
    {
        [InspectableMember("document.title", "Document Title", Order = 1)]
        [InspectablePresentation(Description = "Primary title", Section = "Document", Category = "Identity", HelpText = "Shown on the cover page.")]
        [InspectableEditor(EditorHints.Multiline)]
        [InspectablePersistence("document.title", DatabaseKey = "project.title")]
        [InspectableAccess(VisibleRoles = ["Designer"], EditablePermissions = ["templates.manage"])]
        [InspectableValidation(Required = true, Minimum = 3, Maximum = 120, RegexPattern = "^[A-Za-z ]+$")]
        public string Title
        {
            get => Get<string>() ?? string.Empty;
            set => Set(value);
        }

        [InspectableMember("document.publish", "Publish", Order = 2)]
        [InspectablePresentation(Description = "Publishes the template", Section = "Actions", Category = "Commands")]
        [InspectableAccess(InvokePermissions = ["templates.manage"])]
        public void Publish(int version = 1)
        {
            ApplyExternalValue(nameof(Title), $"{Title} v{version}");
        }

        protected override void OnUpdate()
        {
        }
    }
}
