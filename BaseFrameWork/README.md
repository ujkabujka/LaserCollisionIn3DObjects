# BaseFramework

`BaseFramework` is the reusable dynamic UI foundation used by the main `DocumentAutomation` product.

Its core idea is simple:

- describe editable data with metadata
- keep stable business keys separate from CLR member names
- let hosts render inspectors, commands, nested objects, and runtime-generated forms from that metadata

The framework is still educational. The WPF host is an example gallery that moves from beginner cases to runtime template-driven forms and role-aware metadata.

## Projects

- `BaseFramework.Core`
  Core observable model, metadata model, attributes, access evaluation, stable-key parameter bridge, and reflection/runtime metadata providers.
- `BaseFramework.Generators`
  Source generator that emits inspectable metadata registration for annotated models.
- `BaseFramework.Wpf`
  Reusable WPF inspector, navigation, editor controls, and editor registry.
- `BaseFramework.WpfHost`
  Example gallery for learning and experimentation.
- `BaseFramework.WebHost`
  Lightweight web preview host that still consumes the core metadata path.
- `BaseFramework.Core.Tests`
  Core metadata, stable key, source-generator parity, access-evaluator, and observable-object tests.
- `BaseFramework.Wpf.Tests`
  WPF registry/grouping tests for the reusable inspector host.

## What Changed In This Foundation Pass

- Stable business keys are now first-class. `ParameterBridgeObject` exports inspectable values by stable key by default.
- CLR property names are still accepted on import for backward compatibility, but they are no longer the canonical integration key.
- Metadata is richer. Members can now carry:
  - stable key
  - CLR name
  - display name
  - description/help text
  - category/section
  - editor hint
  - persistence/database keys
  - validation hints
  - access rules for visibility/editability/invocation
  - getter/setter/invoker/value-source delegates
- Reflection still works as the default fallback path.
- The source generator now emits real metadata registrations instead of empty placeholders.
- WPF controls moved into reusable `BaseFramework.Wpf`.
- Editor selection is registry-based rather than buried in one hardcoded switch.
- Runtime-generated forms can use the same inspector through `IRuntimeInspectableMetadataSource`.

## Core Concepts

### 1. Observable Models

Most editable objects derive from:

- `ObservableObject`
  Base observable state container with dependency tracking, layout invalidation, and undo/redo support.
- `ParameterBridgeObject`
  Adds stable-key import/export for inspectable members.

### 2. Inspectable Members

Mark fields or commands with `InspectableMemberAttribute`:

```csharp
[InspectableMember("project.code", "Project Code", Order = 1)]
public string ProjectCode
{
    get => Get<string>() ?? string.Empty;
    set => Set(value);
}
```

The first argument is the stable business key. That is the key you should use for:

- template placeholder mapping
- database mapping
- persistence payloads
- interop between screens/services

Do not treat the CLR property name as the business contract unless you explicitly want legacy compatibility.

### 3. Companion Metadata Attributes

Use small focused companion attributes instead of one giant god attribute:

- `InspectablePresentationAttribute`
  Description, section, category, help text.
- `InspectableAccessAttribute`
  Visible/editable/invokable roles and permissions.
- `InspectableValidationAttribute`
  Required/min/max/regex hints.
- `InspectablePersistenceAttribute`
  Persistence key and database key.
- `InspectableEditorAttribute`
  Explicit editor hint.

Example:

```csharp
[InspectableMember("approval.note", "Approval Note", Order = 3)]
[InspectablePresentation(Section = "Approval", Category = "Restricted")]
[InspectableAccess(
    VisiblePermissions = ["documents.generate"],
    EditablePermissions = ["fields.edit.restricted"])]
[InspectableEditor(EditorHints.Multiline)]
public string ApprovalNote
{
    get => Get<string>() ?? string.Empty;
    set => Set(value);
}
```

## Metadata Resolution

`ReflectionObjectMetadataProvider` resolves metadata in this order:

1. `IRuntimeInspectableMetadataSource`
   If the target builds metadata at runtime, that metadata wins.
2. Generated metadata registry
   If the source generator registered metadata for the target type, it is used.
3. Reflection fallback
   Public inspectable properties and methods are reflected and cached.

This means you can mix:

- classic reflected models
- generated metadata models
- runtime document/template forms

inside the same framework.

## Reflection vs Generated Metadata

Reflection remains the zero-setup path.

Use the source generator when you want:

- less runtime reflection
- compile-time metadata generation
- generated parity with reflected models

Consumer projects opt in by referencing `BaseFramework.Generators` as an analyzer:

```xml
<ProjectReference Include="..\BaseFramework.Generators\BaseFramework.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Then annotate the type:

```csharp
[GenerateInspectorMetadata]
public sealed class ExampleModel : ParameterBridgeObject
{
}
```

If generation is unavailable for a type, the framework still works via reflection.

## Stable Keys And ParameterBridgeObject

Default export:

```csharp
var values = model.GetParameters();
```

This now exports:

- stable inspectable keys only

Legacy-name export still exists:

```csharp
var legacyValues = model.GetParametersByClrName();
```

Import accepts both:

- stable key
- CLR property name

So old callers can still set values while new code migrates to stable keys.

## Permission-Aware UI

Access rules are evaluated by `IMemberAccessEvaluator`.

The default implementation:

- hides members that fail visibility rules
- disables editing when edit rules fail
- disables commands when invoke rules fail

The WPF inspector applies:

- rejection rules from `ObservableObject`
- evaluated access rules
- read-only flags

This is the same foundation the main `DocumentAutomation` app now uses for field-level permissions.

## WPF Editor System

`BaseFramework.Wpf` uses `IInspectorEditorRegistry`.

The default registry maps member kinds and hints to reusable controls such as:

- string
- numeric
- boolean
- enum
- date/time
- note
- nested object
- collection
- method/command
- file picker
- image picker
- table preview

Register a custom editor:

```csharp
var registry = new DefaultInspectorEditorRegistry();
registry.Register("custom.signature", context => new SignatureEditorControl(...));
```

Bind the inspector with explicit access context and registry:

```csharp
inspector.Bind(
    model,
    metadataProvider,
    new DefaultMemberAccessEvaluator(),
    accessContext,
    registry);
```

## Runtime Template-Driven Forms

Static CLR properties are no longer required for every editor scenario.

Implement:

- `IRuntimeInspectableMetadataSource`

and return `InspectableTypeMetadata` built at runtime. This is how template-defined fields can use the same WPF inspector pipeline as ordinary models.

That capability is now used in:

- `BaseFramework.WpfHost` runtime template example
- the main `DocumentAutomation` product's document preparation flow

## Example Gallery

Run `BaseFramework.WpfHost` to explore the progression:

1. `Basic`
   Demonstrates scalar fields, numeric hints, dropdown/value source, multiline string, note editor, computed read-only field, and command invocation.
   Start reading in code:
   `BaseFramework.WpfHost/Models/ExampleGalleryModels.cs` -> `BasicDocumentExampleModel`
2. `Intermediate`
   Demonstrates conditional visibility, nested object inspection, collections, collection-manipulating commands, and computed summaries.
   Start reading in code:
   `BaseFramework.WpfHost/Models/ExampleGalleryModels.cs` -> `NestedWorkflowExampleModel`
3. `Security`
   Demonstrates role-based visibility, permission-based editability, and action-level authorization.
   Start reading in code:
   `BaseFramework.WpfHost/Models/ExampleGalleryModels.cs` -> `RoleAwareExampleModel`
4. `Document Automation`
   Demonstrates runtime-generated metadata that behaves like a scanned template-driven form.
   Start reading in code:
   `BaseFramework.WpfHost/Models/TemplateDrivenExampleForm.cs`
5. `Advanced`
   Demonstrates the larger scheduling example with nested state, collections, calendar concepts, and richer command flows.
   Start reading in code:
   `BaseFramework.WpfHost/Models/InspectorTestHierarchy.cs` -> `Test_Class_3`

## Example Ladder

The repository now teaches the framework in this order:

1. Basic
2. Intermediate
3. Advanced
4. Document automation
5. Security / roles
6. Persistence / DB

The first five are visible in `BaseFramework.WpfHost`.

The persistence / DB example lives in:

- `BaseFrameWorkAndPosgreSQL/EFCoreDemo`
- `src/DocumentAutomation.App` in demo mode or DB-backed mode

## How To Add A New Model

1. Derive from `ObservableObject` or `ParameterBridgeObject`.
2. Mark public properties/methods with `InspectableMemberAttribute`.
3. Add optional presentation/access/editor/validation/persistence attributes.
4. Use stable business keys, not CLR names, for anything external.
5. Bind the object through `ReflectionObjectMetadataProvider` or generated metadata.

## Build And Run

Build the framework solution:

```powershell
dotnet build BaseFrameWork\BaseFramework.sln
```

Run tests:

```powershell
dotnet test BaseFrameWork\BaseFramework.sln
```

Run the WPF gallery:

```powershell
dotnet run --project BaseFrameWork\BaseFramework.WpfHost\BaseFramework.WpfHost.csproj
```

Run the web host:

```powershell
dotnet run --project BaseFrameWork\BaseFramework.WebHost\BaseFramework.WebHost.csproj
```

## Relationship To The Main Product

The main product under `src/` evolves this framework rather than replacing it.

`DocumentAutomation` now uses the same ideas for:

- template field metadata
- field-level permissions
- runtime document forms
- stable template/database keys
- reusable editor composition

So the framework remains the learning surface, and the product projects show how to apply it in a real desktop architecture.
