using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dialogs;

public sealed class DialogSection : UserControl
{
    private readonly DesignTokens.Metrics _metrics;
    private readonly StackPanel _rows;
    private readonly List<DialogRow> _all = [];

    public DialogSection(string? title, DesignTokens.Metrics metrics)
    {
        _metrics = metrics;
        Title = string.IsNullOrWhiteSpace(title) ? null : title;

        _rows = new StackPanel { Spacing = metrics.SpacingMedium };
        var layout = new StackPanel { Spacing = metrics.SpacingSmall };

        if (Title is not null)
        {
            Caption = new TextBlock
            {
                Text = Title.ToUpperInvariant(),
                FontSize = Math.Max(9, metrics.FontSizeSmall - 1),
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                LetterSpacing = 0.6,
                Margin = new Thickness(0, 0, 0, metrics.SpacingSmall / 2),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
            AutomationProperties.SetName(this, Title);
            layout.Children.Add(Caption);
        }

        layout.Children.Add(_rows);
        Content = layout;
    }

    public string? Title { get; }

    public TextBlock? Caption { get; }

    public IReadOnlyList<DialogRow> Rows => _all;

    public double RowSpacing
    {
        get => _rows.Spacing;
        set => _rows.Spacing = value;
    }

    public DialogRow Add(string? label, Control editor, DialogRowKind kind = DialogRowKind.Labelled,
        double editorWidth = DialogLayoutMeasures.EditorWidth, bool alignTop = false) =>
        Add(new DialogRow(label, editor, _metrics, kind, editorWidth, alignTop));

    public DialogRow Add(DialogRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _all.Add(row);
        _rows.Children.Add(row);
        return row;
    }

    public double LabelWidth => _all.Count == 0 ? 0 : _all.Max(row => row.LabelWidth);

    public double SuggestedWidth(double labelWidth)
    {
        var widest = 0.0;
        foreach (var row in _all)
        {
            var width = row.Kind == DialogRowKind.Wide ? row.EditorWidth : labelWidth + _metrics.SpacingMedium + row.EditorWidth;
            widest = Math.Max(widest, width);
        }

        return widest;
    }
}
