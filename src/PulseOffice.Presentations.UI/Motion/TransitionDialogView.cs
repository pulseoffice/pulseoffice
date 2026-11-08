using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Motion;

public sealed record TransitionDialogResult(SlideTransition Transition, bool AllSlides);

public sealed class TransitionDialogView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double PreviewWidth = 256;

    private readonly TransitionEditor _editor;
    private readonly TransitionThumbnail _preview;

    public TransitionDialogView(SlideTransition initial, TransitionEditorContext context, TransitionEditorServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);
        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        _editor = new TransitionEditor(initial, context, services, MotionEditorLayout.Dialog);
        _preview = new TransitionThumbnail(services.Stage, initial, context.Samples, new Size(PreviewWidth, PreviewWidth * 9 / 16), playOnHover: false);
        _editor.Changed += (_, transition) =>
        {
            _preview.Show(transition);
            _preview.Play();
        };

        var replay = new Button { Content = services.Texts["deck.motion.preview"], HorizontalAlignment = HorizontalAlignment.Left };
        Avalonia.Automation.AutomationProperties.SetName(replay, services.Texts["deck.motion.preview"]);
        replay.Click += (_, _) => _preview.Play();

        var frame = new Border
        {
            Child = _preview,
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var previewColumn = new StackPanel { Spacing = metrics.SpacingSmall, Width = PreviewWidth + 2 };
        previewColumn.Children.Add(new TextBlock
        {
            Text = services.Texts["deck.motion.preview"].ToUpperInvariant(),
            FontSize = Math.Max(9, metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        previewColumn.Children.Add(frame);
        previewColumn.Children.Add(replay);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingLarge };
        Grid.SetColumn(previewColumn, 1);
        layout.Children.Add(_editor);
        layout.Children.Add(previewColumn);
        Content = layout;

        Footer = new DialogFooter()
            .WithCancel(services.Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(services.Texts["deck.motion.applyAll"], () => Completed?.Invoke(this, new TransitionDialogResult(_editor.Transition, true)))
            .WithAction(services.Texts["deck.motion.apply"], () => Completed?.Invoke(this, new TransitionDialogResult(_editor.Transition, false)), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public TransitionEditor Editor => _editor;

    public TransitionThumbnail Preview => _preview;

    public double? PreferredWidth => 1100;
}
