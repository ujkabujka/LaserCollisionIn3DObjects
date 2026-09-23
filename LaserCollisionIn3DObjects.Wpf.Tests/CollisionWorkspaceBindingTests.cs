using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using LaserCollisionIn3DObjects.Wpf.Converters;
using LaserCollisionIn3DObjects.Wpf.Views;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Services;
using HelixToolkit.Wpf;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class CollisionWorkspaceBindingTests
{
    [Fact]
    public void CollisionWorkspace_LoadsWithLocalNumericConverterResource()
    {
        StaTest.Run(() =>
        {
            var view = new CollisionWorkspaceView();

            Assert.IsType<FlexibleNumericConverter>(view.Resources["FlexibleNumericConverter"]);
        });
    }

    [Fact]
    public void CollisionProgressBinding_IsOneWay_ForReadOnlyProgressProperty()
    {
        StaTest.Run(() =>
        {
            var view = new CollisionWorkspaceView();
            var progressBar = Assert.IsType<ProgressBar>(view.FindName("CollisionProgressBar"));
            var valueBinding = BindingOperations.GetBinding(progressBar, ProgressBar.ValueProperty);
            var indeterminateBinding = BindingOperations.GetBinding(progressBar, ProgressBar.IsIndeterminateProperty);

            Assert.NotNull(valueBinding);
            Assert.Equal(nameof(ReadOnlyProgressDataContext.CollisionProgressPercent), valueBinding.Path.Path);
            Assert.Equal(BindingMode.OneWay, valueBinding.Mode);
            Assert.NotNull(indeterminateBinding);
            Assert.Equal(BindingMode.OneWay, indeterminateBinding.Mode);
        });
    }

    [Fact]
    public void MeasuredPrismImportButton_UsesCommandBinding()
    {
        StaTest.Run(() =>
        {
            var view = new CollisionWorkspaceView();
            var button = Assert.IsType<Button>(view.FindName("ImportMeasuredPrismsCsvButton"));
            var binding = BindingOperations.GetBinding(button, Button.CommandProperty);
            Assert.Equal(nameof(MainWindowViewModel.ImportMeasuredPrismsCsvCommand), binding?.Path.Path);
        });
    }

    [Fact]
    public void MeasuredPrismImport_AppendsPreservesSourceCornersAndInvalidatesTransactionally()
    {
        StaTest.Run(() =>
        {
            var collisionViewport = new HelixViewport3D();
            var projectionViewport = new HelixViewport3D();
            var vm = new MainWindowViewModel(
                new SceneRenderSyncService(collisionViewport),
                new ProjectionRenderSyncService(projectionViewport));
            var scene = Assert.IsType<CollisionSceneViewModel>(vm.SelectedScene);
            Assert.True(vm.ImportMeasuredPrismsCsvCommand.CanExecute(null));
            vm.SelectedScene = null;
            Assert.False(vm.ImportMeasuredPrismsCsvCommand.CanExecute(null));
            vm.SelectedScene = scene;
            scene.Prisms.Add(new PrismItemViewModel { Name = "Existing" });
            scene.AssignSource(new CollisionSourceLibraryItemViewModel
            {
                GeneratedSource = new CylindricalLightSourceItemViewModel { Name = "Source A" }
            });
            var source = scene.AssignedSource;
            scene.PublishCollisionResults([], []);

            Assert.True(vm.ImportMeasuredPrisms(new StringReader(string.Join('\n', Enumerable.Repeat(ValidRow, 3)))));
            Assert.Equal(4, scene.Prisms.Count);
            Assert.Equal(12, scene.MeasuredCornerPoints.Count);
            Assert.Same(source, scene.AssignedSource);
            Assert.False(scene.HasValidCollisionRun);
            Assert.All(scene.Prisms.Skip(1), prism => Assert.StartsWith("Measured Prism", prism.Name));

            var prismCount = scene.Prisms.Count;
            var cornerCount = scene.MeasuredCornerPoints.Count;
            Assert.False(vm.ImportMeasuredPrisms(new StringReader($"{ValidRow}\ninvalid")));
            Assert.Equal(prismCount, scene.Prisms.Count);
            Assert.Equal(cornerCount, scene.MeasuredCornerPoints.Count);
        });
    }

    private const string ValidRow = "2000,3000,10,10,0,0,10.198039,11.309932,0,10.630146,11.309932,16.392523,10.440307,0,16.699244";

    [Fact]
    public void CollisionWorkspace_LoadsWithoutReadOnlyProgressBindingException()
    {
        StaTest.Run(() =>
        {
            var bindingErrors = new BindingErrorTraceListener();
            PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

            Exception? dispatcherException = null;
            var dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherUnhandledExceptionEventHandler handler = (_, args) =>
            {
                dispatcherException = args.Exception;
                args.Handled = true;
            };
            dispatcher.UnhandledException += handler;

            try
            {
                var view = new CollisionWorkspaceView
                {
                    DataContext = new ReadOnlyProgressDataContext()
                };
                var window = new Window
                {
                    Content = view,
                    Width = 1000,
                    Height = 700,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };

                try
                {
                    window.Show();
                    view.Measure(new Size(1000, 700));
                    view.Arrange(new Rect(0, 0, 1000, 700));
                    view.UpdateLayout();
                    dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                }
                finally
                {
                    window.Close();
                }

                Assert.Null(dispatcherException);
                Assert.DoesNotContain(bindingErrors.Messages, message =>
                    message.Contains("read-only property", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("CheckReadOnly", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                dispatcher.UnhandledException -= handler;
                PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingErrors);
            }
        });
    }

    private sealed class ReadOnlyProgressDataContext
    {
        public double CollisionProgressPercent => 42d;
        public bool CollisionProgressIsIndeterminate => false;
        public bool IsCollisionProgressVisible => true;
        public string CollisionProgressMessage => "Testing collision progress";
        public bool IsCollisionEditingEnabled => false;

        // Empty collections keep the real workspace templates lightweight while bindings activate.
        public IEnumerable CollisionScenes => Array.Empty<object>();
        public IEnumerable Prisms => Array.Empty<object>();
        public IEnumerable HitResults => Array.Empty<object>();
    }

    private sealed class BindingErrorTraceListener : TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
            if (message is not null)
            {
                Messages.Add(message);
            }
        }

        public override void WriteLine(string? message) => Write(message);
    }

    private static class StaTest
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        public static void Run(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Assert.True(thread.Join(Timeout), $"WPF STA test did not complete within {Timeout}.");
            if (exception is not null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }
    }
}
