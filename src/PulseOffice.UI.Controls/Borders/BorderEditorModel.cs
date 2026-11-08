using PulseOffice.Document.Borders;

namespace PulseOffice.UI.Controls.Borders;

public sealed class BorderEditorModel
{
    private readonly IBorderFrameEditing _editing;

    public BorderEditorModel(BorderFrameScope scope, BorderFrame initial, IBorderPresetCatalog presets, IBorderFrameEditing editing)
    {
        ArgumentNullException.ThrowIfNull(presets);
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Initial = initial ?? throw new ArgumentNullException(nameof(initial));
        _editing = editing ?? throw new ArgumentNullException(nameof(editing));
        Frame = initial;
        Presets = presets.For(scope);
        var sample = scope.Positions.Select(initial.Stroke).FirstOrDefault(stroke => stroke is not null);
        Stroke = sample is not null && scope.Dashes.Contains(sample.Dash) ? sample : new BorderStroke(scope.Dashes[0], DefaultWidth, DefaultColor);
    }

    public const double DefaultWidth = 0.75;

    public const string DefaultColor = "#000000";

    public BorderFrameScope Scope { get; }

    public BorderFrame Initial { get; }

    public BorderFrame Frame { get; private set; }

    public BorderStroke Stroke { get; private set; }

    public IReadOnlyList<IBorderPreset> Presets { get; }

    public bool IsChanged => Frame != Initial;

    public event EventHandler? Changed;

    public void Toggle(BorderPosition position) => Set(_editing.Toggle(Frame, Scope, position, Stroke));

    public void Apply(IBorderPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Set(preset.Apply(Frame, Scope, Stroke));
    }

    public void Reset() => Set(Initial);

    public void SetDash(BorderDash dash) => Restroke(Stroke with { Dash = Scope.Dashes.Contains(dash) ? dash : Scope.Dashes[0] });

    public void SetWidth(double points)
    {
        if (double.IsFinite(points)) Restroke(Stroke with { WidthPoints = Math.Clamp(points, 0.25, 12) });
    }

    public void SetColor(string hex) => Restroke(Stroke with { ColorHex = string.IsNullOrWhiteSpace(hex) ? DefaultColor : hex });

    private void Restroke(BorderStroke stroke)
    {
        if (stroke == Stroke) return;
        Stroke = stroke;
        Frame = _editing.Restroke(Frame, stroke);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(BorderFrame frame)
    {
        Frame = frame;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
