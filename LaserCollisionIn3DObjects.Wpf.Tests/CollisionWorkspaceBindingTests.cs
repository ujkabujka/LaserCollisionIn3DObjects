using System.Collections;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using LaserCollisionIn3DObjects.Wpf.Converters;
using LaserCollisionIn3DObjects.Wpf.Views;
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
