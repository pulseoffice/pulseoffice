using PulseOffice.Foundation.Units;

namespace PulseOffice.Document.Forms;

[Flags]
public enum FormPropertyGroup
{
    None = 0,
    Text = 1,
    Numeric = 2,
    DateTime = 4,
    List = 8,
    Image = 16,
    Button = 32,
    Scroll = 64,
    Appearance = 128,
    Data = 256,
}

public sealed record FormControlDescriptor(
    FormControlKind Kind,
    DocumentSize DefaultSize,
    bool HoldsItems,
    bool HoldsNumber,
    bool HoldsText,
    bool IsContainer,
    bool TakesFocus)
{
    public FormPropertyGroup Groups { get; init; } = FormPropertyGroup.Appearance;

    public IReadOnlyList<FormControlEvent> Events { get; init; } = [];
}

public interface IFormControlCatalog
{
    IReadOnlyList<FormControlDescriptor> All { get; }

    FormControlDescriptor Describe(FormControlKind kind);

    FormControlProperties Create(FormControlKind kind, string name);
}

public sealed class FormControlCatalog : IFormControlCatalog
{
    private readonly Dictionary<FormControlKind, FormControlDescriptor> _byKind;

    public FormControlCatalog()
    {
        All = [.. Descriptors()];
        _byKind = All.ToDictionary(descriptor => descriptor.Kind);
    }

    public IReadOnlyList<FormControlDescriptor> All { get; }

    public FormControlDescriptor Describe(FormControlKind kind) =>
        _byKind.TryGetValue(kind, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(kind));

    public FormControlProperties Create(FormControlKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var descriptor = Describe(kind);

        return new FormControlProperties
        {
            Name = name,
            Label = descriptor.HoldsText || descriptor.IsContainer ? name : null,
            TabStop = descriptor.TakesFocus,
            Range = descriptor.HoldsNumber ? DefaultRange(kind) : null,
        };
    }

    private static NumericRange DefaultRange(FormControlKind kind) => kind switch
    {
        FormControlKind.ScrollBar => new NumericRange(0, 100, 1, 10),
        FormControlKind.SpinButton => new NumericRange(0, 100, 1, 10),

        _ => new NumericRange(double.MinValue, double.MaxValue, 1, 10),
    };

    private static readonly FormControlEvent[] Common =
    [
        FormControlEvent.FocusGained, FormControlEvent.FocusLost,
        FormControlEvent.MouseEntered, FormControlEvent.MouseExited,
        FormControlEvent.KeyPressed,
    ];

    private static FormControlEvent[] With(params FormControlEvent[] specific) => [.. specific, .. Common];

    private static IEnumerable<FormControlDescriptor> Descriptors()
    {
        static DocumentSize Size(double width, double height) =>
            new(Twip.FromCentimeters(width), Twip.FromCentimeters(height));

        yield return new(FormControlKind.Label, Size(3, 0.6), false, false, true, false, false)
        {
            Groups = FormPropertyGroup.Appearance,
            Events = With(FormControlEvent.Clicked),
        };
        yield return new(FormControlKind.TextBox, Size(4, 0.7), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.Text | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.TextChanged, FormControlEvent.Approve),
        };
        yield return new(FormControlKind.CheckBox, Size(3.5, 0.6), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged, FormControlEvent.Clicked),
        };
        yield return new(FormControlKind.OptionButton, Size(3.5, 0.6), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged, FormControlEvent.Clicked),
        };
        yield return new(FormControlKind.ListBox, Size(4, 2.6), true, false, false, false, true)
        {
            Groups = FormPropertyGroup.List | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ItemSelected, FormControlEvent.DoubleClicked),
        };
        yield return new(FormControlKind.ComboBox, Size(4, 0.7), true, false, true, false, true)
        {
            Groups = FormPropertyGroup.List | FormPropertyGroup.Text | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ItemSelected, FormControlEvent.TextChanged),
        };
        yield return new(FormControlKind.PushButton, Size(3, 0.8), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.Button | FormPropertyGroup.Appearance,
            Events = With(FormControlEvent.Clicked, FormControlEvent.DoubleClicked, FormControlEvent.Approve, FormControlEvent.Reset),
        };
        yield return new(FormControlKind.ImageButton, Size(2, 2), false, false, false, false, true)
        {
            Groups = FormPropertyGroup.Image | FormPropertyGroup.Button | FormPropertyGroup.Appearance,
            Events = With(FormControlEvent.Clicked),
        };
        yield return new(FormControlKind.FormattedField, Size(4, 0.7), false, true, true, false, true)
        {
            Groups = FormPropertyGroup.Numeric | FormPropertyGroup.Text | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged, FormControlEvent.TextChanged),
        };
        yield return new(FormControlKind.DateField, Size(4, 0.7), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.DateTime | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged),
        };
        yield return new(FormControlKind.TimeField, Size(3, 0.7), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.DateTime | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged),
        };
        yield return new(FormControlKind.NumericField, Size(3, 0.7), false, true, false, false, true)
        {
            Groups = FormPropertyGroup.Numeric | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged),
        };
        yield return new(FormControlKind.CurrencyField, Size(3, 0.7), false, true, false, false, true)
        {
            Groups = FormPropertyGroup.Numeric | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged),
        };
        yield return new(FormControlKind.PatternField, Size(3, 0.7), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.Text | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.TextChanged),
        };
        yield return new(FormControlKind.GroupBox, Size(5, 3), false, false, true, true, false)
        {
            Groups = FormPropertyGroup.Appearance,
            Events = [],
        };
        yield return new(FormControlKind.ImageControl, Size(3, 3), false, false, false, false, false)
        {
            Groups = FormPropertyGroup.Image | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.Clicked),
        };
        yield return new(FormControlKind.FileSelection, Size(5, 0.7), false, false, true, false, true)
        {
            Groups = FormPropertyGroup.Text | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.TextChanged),
        };
        yield return new(FormControlKind.TableControl, Size(8, 4), true, false, false, true, true)
        {
            Groups = FormPropertyGroup.List | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ItemSelected),
        };
        yield return new(FormControlKind.NavigationBar, Size(6, 0.8), false, false, false, false, true)
        {
            Groups = FormPropertyGroup.Appearance,
            Events = With(FormControlEvent.Clicked),
        };
        yield return new(FormControlKind.SpinButton, Size(1, 0.8), false, true, false, false, true)
        {
            Groups = FormPropertyGroup.Numeric | FormPropertyGroup.Scroll | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged),
        };
        yield return new(FormControlKind.ScrollBar, Size(6, 0.6), false, true, false, false, true)
        {
            Groups = FormPropertyGroup.Numeric | FormPropertyGroup.Scroll | FormPropertyGroup.Appearance | FormPropertyGroup.Data,
            Events = With(FormControlEvent.ValueChanged),
        };
    }
}
