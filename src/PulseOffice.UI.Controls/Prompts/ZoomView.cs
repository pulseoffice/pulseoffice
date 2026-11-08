using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Prompts;

public sealed class ZoomView : UserControl, IDialogContent, IDialogFooterOwner
{
    private static readonly double[] Presets = [50, 75, 100, 150, 200];

    private readonly ZoomRequest _request;
    private readonly IReadOnlyList<double> _presets;
    private readonly IconChoiceGroup _percentages;
    private readonly IconChoiceGroup? _fits;
    private readonly MeasureField _percent;
    private readonly ZoomGlyph _glyph = new() { Width = 200, Height = 140 };
    private bool _syncing;

    public ZoomView(ZoomRequest request, ITextCatalog texts, IMeasureUnits units, IIconCatalog? icons, CultureInfo culture,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _glyph.PageAspect = request.PageAspect;
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);

        _presets = [.. Presets.Where(value => value >= request.MinimumPercent && value <= request.MaximumPercent)];
        _percentages = new IconChoiceGroup(
            [.. _presets.Select(value => new IconChoice(value.ToString(culture), string.Empty, $"{value.ToString(culture)} %"))],
            null, metrics, texts["zoom.presets"], -1, showLabels: true);

        if (request.Fits.Count > 0)
            _fits = new IconChoiceGroup(
                [.. request.Fits.Select(fit => new IconChoice(fit.ToString(), FitIcon(fit), texts[$"zoom.fit.{fit.ToString().ToLowerInvariant()}"]))],
                icons, metrics, texts["zoom.fits"], -1, showLabels: true);

        _percent = new MeasureField(
            new MeasureEntry(new MeasureOptions("%", request.MinimumPercent, request.MaximumPercent, 5)
            {
                LargeStep = 25,
                Integer = true,
                ShowSlider = true,
            }, units, culture),
            Math.Round(request.CurrentPercent), texts, metrics, icons, texts["zoom.percent"], boxWidth: 56)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var layout = new TwoPaneDialogLayout(metrics)
        {
            Header = new HelpTip(texts["zoom.explanation"], texts, metrics),
            Preview = _glyph,
        };

        var section = layout.AddSection((string?)null);
        section.Add(texts["zoom.presets"], _percentages, editorWidth: 300);
        if (_fits is not null) section.Add(texts["zoom.fits"], _fits, editorWidth: 300);
        var row = section.Add(texts["zoom.percent"], _percent, editorWidth: 300);
        if (row.Label is { } handle) _percent.AttachScrubber(handle);

        Content = layout;

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], Confirm, primary: true);

        _percentages.SelectionChanged += (_, index) =>
        {
            if (_syncing || index < 0) return;
            Sync(() =>
            {
                _percent.Value = _presets[index];
                _fits?.SelectedIndex = -1;
            });
        };

        _fits?.SelectionChanged += (_, index) =>
        {
            if (_syncing || index < 0) return;
            Sync(() => _percentages.SelectedIndex = -1);
        };

        _percent.ValueCommitted += (_, value) =>
        {
            if (_syncing) return;
            Sync(() =>
            {
                _percentages.SelectedIndex = PresetIndex(value);
                _fits?.SelectedIndex = -1;
            });
        };

        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Confirm();
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Bubble);

        Sync(() =>
        {
            if (request.CurrentFit is { } fit && _fits is not null && request.Fits.Contains(fit))
                _fits.SelectedIndex = request.Fits.ToList().IndexOf(fit);
            else
                _percentages.SelectedIndex = PresetIndex(_percent.Value);
        });
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public IconChoiceGroup Percentages => _percentages;

    public IconChoiceGroup? Fits => _fits;

    public MeasureField Percent => _percent;

    public ZoomGlyph Glyph => _glyph;

    public ZoomAnswer Answer =>
        _fits is { SelectedIndex: >= 0 } fits
            ? ZoomAnswer.Fitting(_request.Fits[fits.SelectedIndex])
            : ZoomAnswer.At(_percent.Read());

    public void Confirm() => Completed?.Invoke(this, Answer);

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var focus = _percentages.SelectedIndex >= 0 ? _percentages.Items[_percentages.SelectedIndex] : (Control?)null;
        focus?.Focus();
    }

    private void Sync(Action change)
    {
        _syncing = true;
        try
        {
            change();
        }
        finally
        {
            _syncing = false;
        }

        _glyph.Show(Answer.Percent ?? _request.CurrentPercent, Answer.Fit);
    }

    private int PresetIndex(double value) =>
        _presets.ToList().FindIndex(preset => Math.Abs(preset - value) < 0.5);

    private string FitIcon(ZoomFit fit) => fit switch
    {
        ZoomFit.Page => "ledger.zoom.page",
        _ => "ledger.zoom.pageWidth",
    };
}

public sealed class ZoomGlyph : Control
{
    private const double PageAtHundred = 0.62;

    private double _percent = 100;
    private double _aspect = 1.414;
    private ZoomFit? _fit;

    public ZoomGlyph()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public double ShownPercent => _percent;

    public ZoomFit? ShownFit => _fit;

    public double PageAspect
    {
        get => _aspect;
        set
        {
            _aspect = value > 0 ? value : 1.414;
            InvalidateVisual();
        }
    }

    public void Show(double percent, ZoomFit? fit)
    {
        _percent = percent;
        _fit = fit;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var frame = new Rect(Bounds.Size).Deflate(1);
        var canvas = Brush(Palette.Keys.Canvas, Colors.LightGray);
        var border = new Pen(Brush(Palette.Keys.BorderStrong, Colors.Gray), 1);
        context.DrawRectangle(canvas, border, frame, 6, 6);

        var (width, height) = PageSize(frame);
        var left = frame.X + (frame.Width - width) / 2;
        var top = _fit == ZoomFit.Width || height > frame.Height - 12 ? frame.Y + 10 : frame.Y + (frame.Height - height) / 2;
        var page = new Rect(left, top, width, height);

        using (context.PushClip(new RoundedRect(frame, 6)))
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), null, page.Translate(new Vector(1.5, 2)));
            context.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 0.5), page);

            var lines = new Pen(new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xD2)), Math.Max(1, height / 90));
            var margin = width * 0.12;
            var step = Math.Max(3, height / 22);
            var y = page.Y + height * 0.1;
            var index = 0;
            while (y < page.Bottom - height * 0.1 && y < frame.Bottom)
            {
                var length = (index++ % 5 == 4 ? 0.55 : 1) * (width - 2 * margin);
                context.DrawLine(lines, new Point(page.X + margin, y), new Point(page.X + margin + length, y));
                y += step;
            }
        }
    }

    private (double Width, double Height) PageSize(Rect frame)
    {
        var (width, height) = _fit switch
        {
            ZoomFit.Page => (frame.Height * 0.86 / _aspect, frame.Height * 0.86),
            ZoomFit.Width => (frame.Width * 0.9, frame.Width * 0.9 * _aspect),
            _ => (frame.Height * PageAtHundred * _percent / 100 / _aspect, frame.Height * PageAtHundred * _percent / 100),
        };
        var shrink = _fit == ZoomFit.Page && width > frame.Width * 0.9 ? frame.Width * 0.9 / width : 1;
        return (width * shrink, height * shrink);
    }

    private IBrush Brush(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : new SolidColorBrush(fallback);
}
