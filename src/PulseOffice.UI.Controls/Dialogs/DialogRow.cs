using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dialogs;

public enum DialogRowKind { Labelled, Indented, Wide }

public sealed class DialogRow : Grid
{
    public const string LabelColumnGroup = "PulseDialogLabel";

    private readonly DesignTokens.Metrics _metrics;
    private bool? _stacked;

    public DialogRow(string? label, Control editor, DesignTokens.Metrics metrics, DialogRowKind kind = DialogRowKind.Labelled,
        double editorWidth = DialogLayoutMeasures.EditorWidth, bool alignTop = false)
    {
        ArgumentNullException.ThrowIfNull(editor);

        _metrics = metrics;
        Kind = kind;
        Editor = editor;
        EditorWidth = editorWidth;
        AlignTop = alignTop;

        ColumnDefinitions =
        [
            new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = LabelColumnGroup },
            new ColumnDefinition(GridLength.Star),
        ];
        RowDefinitions = new RowDefinitions("Auto,Auto");
        ColumnSpacing = metrics.SpacingMedium;

        if (kind == DialogRowKind.Labelled && !string.IsNullOrEmpty(label))
        {
            Label = new TextBlock
            {
                Text = label,
                FontSize = metrics.FontSize,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = DialogLayoutMeasures.LabelMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = alignTop ? VerticalAlignment.Top : VerticalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
            Children.Add(Label);
            if (string.IsNullOrEmpty(AutomationProperties.GetName(editor))) AutomationProperties.SetName(editor, label);
            AutomationProperties.SetLabeledBy(editor, Label);
        }

        Children.Add(editor);
        Place(stacked: false);
    }

    public DialogRowKind Kind { get; }

    public TextBlock? Label { get; }

    public Control Editor { get; }

    public double EditorWidth { get; }

    public bool AlignTop { get; }

    public bool IsStacked => _stacked == true;

    public double LabelWidth
    {
        get
        {
            if (Label is null) return 0;
            Label.Measure(Size.Infinity);
            return Math.Min(DialogLayoutMeasures.LabelMaxWidth, Label.DesiredSize.Width);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var stacked = Kind == DialogRowKind.Labelled && Label is not null
            && !double.IsInfinity(availableSize.Width)
            && availableSize.Width < DialogLayoutMeasures.StackRowsUnder;
        Place(stacked);
        return base.MeasureOverride(availableSize);
    }

    private void Place(bool stacked)
    {
        if (_stacked == stacked) return;
        _stacked = stacked;

        if (Label is not null)
        {
            SetRow(Label, 0);
            SetColumn(Label, 0);
            SetColumnSpan(Label, stacked ? 2 : 1);
            Label.Margin = stacked
                ? new Thickness(0, 0, 0, _metrics.SpacingSmall / 2)
                : AlignTop ? new Thickness(0, _metrics.SpacingSmall, 0, 0) : default;
        }

        var spanning = stacked || Kind == DialogRowKind.Wide;
        SetRow(Editor, stacked && Label is not null ? 1 : 0);
        SetColumn(Editor, spanning ? 0 : 1);
        SetColumnSpan(Editor, spanning ? 2 : 1);
    }
}
