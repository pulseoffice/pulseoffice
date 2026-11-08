using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckColorBar(DeckViewState state)
{
    private readonly DeckViewState _state = state;
    private readonly DeckActions _actions = state.Actions;
    private readonly ITextCatalog _texts = state.Texts;
    private readonly SlideView _canvas = state.Canvas;

    private readonly WrapPanel _colorBar = new() { IsVisible = false, Margin = new Thickness(0, 8, 0, 0) };

    public WrapPanel Panel => _colorBar;

    private static readonly string[] ColorBarColors =
    [
        "#000000", "#404040", "#808080", "#C0C0C0", "#FFFFFF", "#DC2626", "#EA580C", "#F59E0B", "#FACC15",
        "#16A34A", "#0D9488", "#0891B2", "#2563EB", "#4F46E5", "#7C3AED", "#C026D3", "#DB2777", "#78350F",
    ];

    public void BuildColorBar()
    {
        foreach (var hex in ColorBarColors)
        {
            var swatch = new Button
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 4, 4),
                Background = new SolidColorBrush(Color.Parse(hex)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                Classes = { "swatch" },
            };
            ToolTip.SetTip(swatch, _texts["deck.colorBar.tip"]);
            swatch.Click += (_, e) => ApplyBarColor(hex, line: false);
            swatch.PointerReleased += (_, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right) ApplyBarColor(hex, line: true);
            };
            _colorBar.Children.Add(swatch);
        }
    }

    private void ApplyBarColor(string hex, bool line)
    {
        if (_state.Current is not { } slide) return;
        var painted = new List<SlideElement>();
        foreach (var element in _canvas.Selection.OfType<ObjectElement>())
        {
            PulseOffice.Document.Embedding.EmbeddedObject? updated = element.Content switch
            {
                PulseOffice.Drawing.Shapes.ShapeObject shape => shape with { Style = Paint(shape.Style, hex, line) },
                PulseOffice.Drawing.Freeform.FreeformObject freeform => freeform with { Style = Paint(freeform.Style, hex, line) },
                _ => null,
            };
            if (updated is not null) painted.Add(element with { Content = updated });
        }
        if (painted.Count > 0) _actions.ReplaceElements(slide, painted, "deck.colorBar.action");
    }

    private PulseOffice.Drawing.Shapes.ShapeStyle Paint(PulseOffice.Drawing.Shapes.ShapeStyle style, string hex, bool line) =>
        line
            ? style with { Line = style.Line with { ColorHex = hex, Dash = style.Line.Dash == PulseOffice.Drawing.Shapes.ShapeStrokeStyle.None ? PulseOffice.Drawing.Shapes.ShapeStrokeStyle.Solid : style.Line.Dash } }
            : style with { Fill = style.Fill with { Kind = PulseOffice.Drawing.Shapes.ShapeFillKind.Solid, ColorHex = hex } };
}
