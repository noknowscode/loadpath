using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Loadpath.Controls;

/// <summary>
/// Mono numeric field with a unit suffix. Commits on Enter or blur, reverts on Esc, steps with Up/Down (×10 with Shift).
/// Value is the committed number, shown and typed in the current culture like every other number on screen ("1,500" in
/// French); only files use the invariant culture.
/// </summary>
public sealed partial class NumberField : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumberField), new PropertyMetadata(0.0, (d, _) => ((NumberField)d).RenderValue()));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(NumberField), new PropertyMetadata(""));

    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(NumberField), new PropertyMetadata(2, (d, _) => ((NumberField)d).RenderValue()));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(NumberField), new PropertyMetadata(0.5));

    private TextBox? _box;
    private bool _editing;

    public NumberField()
    {
        DefaultStyleKey = typeof(NumberField);
        IsTabStop = false;
    }

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_box is not null)
        {
            _box.KeyDown -= OnKeyDown;
            _box.LostFocus -= OnLostFocus;
            _box.GotFocus -= OnGotFocus;
        }
        _box = GetTemplateChild("PART_Box") as TextBox;
        if (_box is not null)
        {
            _box.KeyDown += OnKeyDown;
            _box.LostFocus += OnLostFocus;
            _box.GotFocus += OnGotFocus;
        }
        RenderValue();
    }

    private void RenderValue()
    {
        if (_box is null || _editing) return;
        _box.Text = Format(Value, Decimals, CultureInfo.CurrentCulture);
    }

    /// <summary>The text shown for a value.</summary>
    public static string Format(double value, int decimals, CultureInfo culture) => value.ToString("F" + decimals, culture);

    /// <summary>
    /// Read typed text. A typographic minus counts as a minus (.NET also takes "-" where the culture's sign is "−").
    /// False for anything else, NaN and infinity included.
    /// </summary>
    public static bool TryParse(string text, CultureInfo culture, out double value) =>
        double.TryParse(text.Trim().Replace("−", "-"), NumberStyles.Float, culture, out value) && double.IsFinite(value);

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        _editing = true;
        _box?.SelectAll();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        _editing = false;
        Commit();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                Commit();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                _editing = false;
                RenderValue();
                e.Handled = true;
                break;
            case VirtualKey.Up:
            case VirtualKey.Down:
                var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                var delta = (e.Key == VirtualKey.Up ? 1 : -1) * Step * (shift ? 10 : 1);
                _editing = false;
                Value = Math.Round(Value + delta, Math.Max(Decimals, 3));
                _editing = true;
                _box?.SelectAll();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Commit the number being typed, if there is one. A press on the canvas calls this before it changes the selection:
    /// losing focus commits too, but that arrives after the press, when the field is already bound to the next element.
    /// </summary>
    public static void CommitFocused(XamlRoot root)
    {
        for (var e = FocusManager.GetFocusedElement(root) as DependencyObject; e is not null; e = VisualTreeHelper.GetParent(e))
        {
            if (e is NumberField field) { field.Commit(); return; }
        }
    }

    private void Commit()
    {
        if (_box is null) return;
        _editing = false;
        // Text that is not a number puts the last value back.
        if (TryParse(_box.Text, CultureInfo.CurrentCulture, out var v)) Value = Math.Round(v, Math.Max(Decimals, 3));
        RenderValue();
    }
}
