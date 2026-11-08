namespace PulseOffice.Document.Forms;

public interface IFormControlInteraction
{
    bool Responds(FormControlKind kind);

    FormControlProperties? Press(
        FormControlKind kind,
        FormControlProperties control,
        double relativeX,
        double relativeY,
        int itemCount,
        bool horizontal,
        double height = 0);

    FormControlProperties? Drag(
        FormControlKind kind,
        FormControlProperties control,
        double relativeX,
        double relativeY,
        bool horizontal);
}

public sealed class FormControlInteraction : IFormControlInteraction
{
    private readonly IReadOnlyList<ControlBehaviour> _behaviours =
    [
        new CheckBoxBehaviour(), new OptionBehaviour(), new ButtonBehaviour(FormControlKind.PushButton),
        new ButtonBehaviour(FormControlKind.ImageButton), new ListBehaviour(), new SpinBehaviour(), new ScrollBehaviour(),
    ];

    public bool Responds(FormControlKind kind) => Behaviour(kind) is not null;

    public FormControlProperties? Press(
        FormControlKind kind, FormControlProperties control, double relativeX, double relativeY, int itemCount, bool horizontal,
        double height = 0)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!control.Enabled || control.ReadOnly) return null;

        return Behaviour(kind) is { } behaviour
            ? behaviour.Press(control, new ControlPointer(relativeX, relativeY, itemCount, horizontal, height))
            : control;
    }

    public FormControlProperties? Drag(
        FormControlKind kind, FormControlProperties control, double relativeX, double relativeY, bool horizontal)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!control.Enabled || control.ReadOnly) return null;

        return Behaviour(kind)?.Drag(control, new ControlPointer(relativeX, relativeY, 0, horizontal, 0));
    }

    private ControlBehaviour? Behaviour(FormControlKind kind) => _behaviours.FirstOrDefault(behaviour => behaviour.Kind == kind);
}

internal readonly record struct ControlPointer(double X, double Y, int ItemCount, bool Horizontal, double Height)
{
    public double Along => Horizontal ? X : Y;
}

internal abstract class ControlBehaviour
{
    protected static readonly NumericRange DefaultRange = new(0, 100, 1, 10);

    public abstract FormControlKind Kind { get; }

    public abstract FormControlProperties? Press(FormControlProperties control, ControlPointer pointer);

    public virtual FormControlProperties? Drag(FormControlProperties control, ControlPointer pointer) => null;

    protected FormControlProperties WithValue(FormControlProperties control, FormControlValue value) => control with { Value = value };
}

internal sealed class CheckBoxBehaviour : ControlBehaviour
{
    public override FormControlKind Kind => FormControlKind.CheckBox;

    public override FormControlProperties? Press(FormControlProperties control, ControlPointer pointer) =>
        WithValue(control, control.Value with
        {
            Checked = control.Value.Checked switch
            {
                CheckState.Unchecked => CheckState.Checked,
                CheckState.Checked => control.TriState ? CheckState.Indeterminate : CheckState.Unchecked,
                _ => CheckState.Unchecked,
            },
        });
}

internal sealed class OptionBehaviour : ControlBehaviour
{
    public override FormControlKind Kind => FormControlKind.OptionButton;

    public override FormControlProperties? Press(FormControlProperties control, ControlPointer pointer) =>
        control.Value.Checked == CheckState.Checked ? null : WithValue(control, control.Value with { Checked = CheckState.Checked });
}

internal sealed class ButtonBehaviour(FormControlKind kind) : ControlBehaviour
{
    public override FormControlKind Kind => kind;

    public override FormControlProperties? Press(FormControlProperties control, ControlPointer pointer) => control;
}

internal sealed class ListBehaviour : ControlBehaviour
{
    private const double FallbackRowFraction = 0.2;

    private readonly ListBoxLayout _layout = new();

    public override FormControlKind Kind => FormControlKind.ListBox;

    public override FormControlProperties? Press(FormControlProperties control, ControlPointer pointer)
    {
        if (pointer.ItemCount == 0) return null;

        var index = pointer.Height > 0
            ? _layout.RowAt(pointer.Y * pointer.Height, pointer.Height)
            : (int)Math.Floor(pointer.Y / FallbackRowFraction);

        return index < 0 || index >= pointer.ItemCount ? null : WithValue(control, control.Value with { SelectedIndices = [index] });
    }
}

internal sealed class SpinBehaviour : ControlBehaviour
{
    public override FormControlKind Kind => FormControlKind.SpinButton;

    public override FormControlProperties? Press(FormControlProperties control, ControlPointer pointer)
    {
        var range = control.Range ?? DefaultRange;
        var current = control.Value.Number ?? range.Minimum;
        var increase = pointer.Horizontal ? pointer.X >= 0.5 : pointer.Y < 0.5;
        return WithValue(control, control.Value with { Number = range.Clamp(current + ((increase ? 1 : -1) * range.Step)) });
    }
}

internal sealed class ScrollBehaviour : ControlBehaviour
{
    public override FormControlKind Kind => FormControlKind.ScrollBar;

    public override FormControlProperties? Press(FormControlProperties control, ControlPointer pointer) => Scroll(control, pointer.Along);

    public override FormControlProperties? Drag(FormControlProperties control, ControlPointer pointer) => Scroll(control, pointer.Along);

    private FormControlProperties Scroll(FormControlProperties control, double along)
    {
        var range = control.Range ?? DefaultRange;
        return WithValue(control, control.Value with { Number = range.Snap(range.Minimum + (Math.Clamp(along, 0, 1) * (range.Maximum - range.Minimum))) });
    }
}
