using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using LaserCollisionIn3DObjects.Wpf.Converters;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class NumericTextBoxBindingTests
{
    [Theory]
    [InlineData("0.07", 0.07)]
    [InlineData("0.005", 0.005)]
    [InlineData("-0.07", -0.07)]
    [InlineData("10.25", 10.25)]
    [InlineData("0,07", 0.07)]
    public void LostFocusBinding_PreservesTextUntilCompleteValueIsCommitted(string enteredText, double expected)
    {
        RunSta(() =>
        {
            var model = new NumericModel();
            var textBox = new TextBox { DataContext = model };
            BindingOperations.SetBinding(textBox, TextBox.TextProperty, new Binding(nameof(NumericModel.Value))
            {
                Converter = new FlexibleNumericConverter(),
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            });

            textBox.Text = enteredText;
            Assert.Equal(enteredText, textBox.Text);
            Assert.Equal(1d, model.Value);

            textBox.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.Equal(expected, model.Value, 6);
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class NumericModel { public double Value { get; set; } = 1d; }
}
