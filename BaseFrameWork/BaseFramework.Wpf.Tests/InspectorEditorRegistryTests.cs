using System.Runtime.ExceptionServices;
using BaseFramework.Core;
using BaseFramework.Core.Access;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;
using BaseFramework.Wpf.Controls;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace BaseFramework.Wpf.Tests;

public sealed class InspectorEditorRegistryTests
{
    [Fact]
    public void DefaultInspectorEditorRegistry_ShouldPreferEditorHintOverMemberKind()
    {
        RunInSta(() =>
        {
            var target = new RuntimeTestObject([]);
            var member = new InspectableMemberMetadata(
                "asset.image",
                "Asset Image",
                MemberKind.String,
                false,
                1,
                typeof(string),
                null,
                null)
            {
                EditorHint = EditorHints.Image,
                Getter = static (instance, metadata) => ((RuntimeTestObject)instance).GetRaw(metadata.Key),
                Setter = static (instance, value, metadata) => ((RuntimeTestObject)instance).ApplyExternalValue(metadata.Key, value)
            };

            var registry = new DefaultInspectorEditorRegistry();
            var editor = registry.CreateEditor(new InspectorEditorContext(
                target,
                member,
                new ReflectionObjectMetadataProvider(),
                new DefaultMemberAccessEvaluator(),
                InspectableAccessContext.Empty,
                registry));

            Assert.IsType<PathPickerEditorControl>(editor);
        });
    }

    [Fact]
    public void ObjectInspectorControl_ShouldGroupMembersBySectionThenCategory()
    {
        RunInSta(() =>
        {
            var metadata = new[]
            {
                CreateMember("beta.second", "Beta Item", "Beta", "Second", 3),
                CreateMember("alpha.first.b", "Alpha First B", "Alpha", "First", 2),
                CreateMember("alpha.second", "Alpha Second", "Alpha", "Second", 1),
                CreateMember("alpha.first.a", "Alpha First A", "Alpha", "First", 1)
            };

            var target = new RuntimeTestObject(metadata);
            var inspector = new ObjectInspectorControl();
            inspector.Bind(
                target,
                new ReflectionObjectMetadataProvider(),
                new DefaultMemberAccessEvaluator(),
                InspectableAccessContext.Empty,
                new RecordingEditorRegistry());

            var rootPanel = Assert.IsType<StackPanel>(inspector.FindName("RootPanel"));
            var labels = rootPanel.Children.OfType<TextBlock>().Select(text => text.Text).ToList();

            Assert.Equal(
            [
                "Alpha",
                "First",
                "editor:Alpha First A",
                "editor:Alpha First B",
                "Second",
                "editor:Alpha Second",
                "Beta",
                "Second",
                "editor:Beta Item"
            ], labels);
        });
    }

    private static InspectableMemberMetadata CreateMember(string key, string displayName, string section, string category, int order)
        => new(
            key,
            displayName,
            MemberKind.String,
            false,
            order,
            typeof(string),
            null,
            null)
        {
            Section = section,
            Category = category,
            Getter = static (instance, metadata) => ((RuntimeTestObject)instance).GetRaw(metadata.Key),
            Setter = static (instance, value, metadata) => ((RuntimeTestObject)instance).ApplyExternalValue(metadata.Key, value)
        };

    private static void RunInSta(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplicationResources();
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static void EnsureApplicationResources()
    {
        var application = System.Windows.Application.Current ?? new System.Windows.Application();
        if (application.Resources.MergedDictionaries.Any(dictionary =>
                dictionary.Source?.OriginalString?.Contains("BaseFramework.Wpf;component/Themes/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) == true))
        {
            return;
        }

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/BaseFramework.Wpf;component/Themes/DarkTheme.xaml", UriKind.Relative)
        });
    }

    private sealed class RecordingEditorRegistry : IInspectorEditorRegistry
    {
        public FrameworkElement? CreateEditor(InspectorEditorContext context)
            => new TextBlock { Text = $"editor:{context.Member.DisplayName}" };

        public void Register(MemberKind kind, Func<InspectorEditorContext, FrameworkElement?> factory)
            => throw new NotSupportedException();

        public void Register(string editorHint, Func<InspectorEditorContext, FrameworkElement?> factory)
            => throw new NotSupportedException();
    }

    private sealed class RuntimeTestObject(IReadOnlyList<InspectableMemberMetadata> members) : ObservableObject, IRuntimeInspectableMetadataSource
    {
        private readonly IReadOnlyList<InspectableMemberMetadata> _members = members;

        public InspectableTypeMetadata GetRuntimeMetadata()
            => new(GetType(), _members);

        protected override void OnUpdate()
        {
        }
    }
}
