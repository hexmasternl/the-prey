using System.Linq;

namespace HexMaster.ThePrey.Maui.App.Controls;

/// <summary>
/// A reusable OTP-style passcode entry. Renders <see cref="Length"/> boxes horizontally, one digit
/// per box, and exposes the entered digits via the two-way <see cref="Code"/> property.
/// <para>
/// Implementation: a single, visually-hidden <see cref="Entry"/> is laid over a row of display boxes
/// and captures every keystroke and paste. Because the OS delivers a pasted string in one
/// <see cref="Entry.TextChanged"/> event, pasting the whole code distributes across the boxes for free.
/// The <see cref="Code"/>-changed handler is idempotent (a no-op when the incoming value already equals
/// the hidden entry's sanitized text) so it does not loop against a view-model setter that re-raises
/// <c>PropertyChanged</c> for an unchanged net value.
/// </para>
/// </summary>
public partial class PasscodeEntry : ContentView
{
    private readonly List<(Border Border, Label Label)> _boxes = new();
    private Style? _boxStyle;
    private Style? _boxActiveStyle;
    private Style? _digitStyle;
    private bool _syncing;

    public static readonly BindableProperty CodeProperty = BindableProperty.Create(
        nameof(Code),
        typeof(string),
        typeof(PasscodeEntry),
        string.Empty,
        BindingMode.TwoWay,
        propertyChanged: OnCodePropertyChanged);

    public static readonly BindableProperty LengthProperty = BindableProperty.Create(
        nameof(Length),
        typeof(int),
        typeof(PasscodeEntry),
        4,
        propertyChanged: OnLengthPropertyChanged);

    public PasscodeEntry()
    {
        InitializeComponent();

        _boxStyle = ResolveStyle("PasscodeBox");
        _boxActiveStyle = ResolveStyle("PasscodeBoxActive");
        _digitStyle = ResolveStyle("PasscodeBoxDigit");

        BuildBoxes();
    }

    /// <summary>The entered digits (two-way). Never holds more than <see cref="Length"/> characters.</summary>
    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>How many digit boxes to render (and the max number of digits accepted).</summary>
    public int Length
    {
        get => (int)GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    private static void OnCodePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        => ((PasscodeEntry)bindable).ApplyCode(newValue as string ?? string.Empty);

    private static void OnLengthPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        => ((PasscodeEntry)bindable).BuildBoxes();

    private Style? ResolveStyle(string key)
        => Application.Current?.Resources.TryGetValue(key, out var value) == true ? value as Style : null;

    private string Sanitize(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).Take(Math.Max(0, Length)).ToArray());

    /// <summary>Rebuilds the display boxes to match <see cref="Length"/>.</summary>
    private void BuildBoxes()
    {
        _boxes.Clear();
        BoxRow.Children.Clear();

        var count = Math.Max(0, Length);
        for (var i = 0; i < count; i++)
        {
            var label = new Label { Style = _digitStyle };
            var border = new Border { Style = _boxStyle, Content = label };
            _boxes.Add((border, label));
            BoxRow.Children.Add(border);
        }

        HiddenEntry.MaxLength = count;
        RefreshDisplay();
    }

    /// <summary>
    /// Applies an externally-set <see cref="Code"/> value. Idempotent: when the sanitized incoming
    /// value already equals the hidden entry's current text it only refreshes the display, which is
    /// what breaks the feedback loop with a re-notifying view-model setter.
    /// </summary>
    private void ApplyCode(string value)
    {
        var sanitized = Sanitize(value);
        var current = Sanitize(HiddenEntry.Text);

        if (sanitized == current)
        {
            RefreshDisplay();
            return;
        }

        _syncing = true;
        HiddenEntry.Text = sanitized;
        HiddenEntry.CursorPosition = sanitized.Length;
        _syncing = false;

        if (Code != sanitized)
        {
            Code = sanitized;
        }

        RefreshDisplay();
    }

    private void OnHiddenEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        var sanitized = Sanitize(e.NewTextValue);

        // If the raw input carried non-digits or overflow, snap the entry back to the clean value.
        if (sanitized != e.NewTextValue)
        {
            _syncing = true;
            HiddenEntry.Text = sanitized;
            HiddenEntry.CursorPosition = sanitized.Length;
            _syncing = false;
        }

        if (Code != sanitized)
        {
            Code = sanitized;
        }

        RefreshDisplay();
    }

    /// <summary>Focuses the hidden entry when the user taps anywhere on the control.</summary>
    private void OnTapped(object? sender, TappedEventArgs e) => HiddenEntry.Focus();

    /// <summary>Repaints each box's digit and highlights the next box to fill.</summary>
    private void RefreshDisplay()
    {
        var text = Sanitize(HiddenEntry.Text);
        for (var i = 0; i < _boxes.Count; i++)
        {
            _boxes[i].Label.Text = i < text.Length ? text[i].ToString() : string.Empty;
            _boxes[i].Border.Style = i == text.Length ? _boxActiveStyle : _boxStyle;
        }
    }
}
