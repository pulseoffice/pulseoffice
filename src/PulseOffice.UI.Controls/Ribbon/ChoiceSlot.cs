using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class ChoiceSlotFactory(IColorContrast contrast) : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is ChoiceSpec;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new ChoiceSlot(item, form, context, contrast);
}

public sealed class ChoiceSlot : RibbonSlot
{
    private readonly ContentControl _face = new();
    private readonly StackPanel _tiles = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly IColorContrast _contrast;
    private Flyout? _flyout;
    private IReadOnlyList<RibbonChoice>? _shownChoices;
    private string? _shownCurrent;

    public ChoiceSlot(RibbonItem item, RibbonForm form, RibbonBuildContext context, IColorContrast contrast)
        : base(item, form, context)
    {
        _contrast = contrast ?? throw new ArgumentNullException(nameof(contrast));
        Initialise();
        ActualThemeVariantChanged += (_, _) =>
        {
            _shownChoices = null;
            Update();
        };
    }

    private ChoiceSpec Spec => (ChoiceSpec)Item.Control!;

    private const string SampleText = "AaBbCc";

    private readonly SampleTypeScale _scale = new();

    private double TileWidth => Spec.TileWidth;

    private double SampleTileWidth => Math.Max(TileWidth, 112);

    private bool Inline => Spec.Look == RibbonChoiceLook.Gallery && Form is RibbonForm.Full or RibbonForm.Medium or RibbonForm.Popover
        && (Form == RibbonForm.Popover || Spec.InlineTiles > 0);

    protected override Control Build()
    {
        _flyout = Context.Popover(Panel);

        return Inline ? BuildGallery() : BuildDropDown();
    }

    private Control BuildDropDown()
    {
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        _face.VerticalAlignment = VerticalAlignment.Center;
        layout.Children.Add(_face);

        if (Context.Chevron(0.6) is { } chevron)
        {
            chevron.VerticalAlignment = VerticalAlignment.Center;
            chevron.Margin = new Thickness(Context.Metrics.SpacingSmall / 2, 0, 0, 0);
            Grid.SetColumn(chevron, 1);
            layout.Children.Add(chevron);
        }

        var compact = Form == RibbonForm.Compact;

        var button = Context.Flat(layout, width: compact ? null : Spec.Width);
        button.Classes.Add("field");
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.Click += (_, _) => Context.Dismissal.Toggle(_flyout!, button);

        return button;
    }

    private Control BuildGallery()
    {
        var more = Context.Flat(Context.Chevron(0.7), width: 18, height: Context.ItemHeight * 2 + 8);
        more.Classes.Add("field");
        more.Padding = default;
        more.Click += (_, _) => Context.Dismissal.Toggle(_flyout!, this);
        ToolTip.SetTip(more, Item.Title);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _tiles, more },
        };
    }

    protected override void Update()
    {
        var choices = Spec.Choices();
        var current = Spec.Current();

        if (Inline)
        {
            if (_shownChoices is not null && _shownCurrent == current && _shownChoices.SequenceEqual(choices))
            {
                ToolTip.SetTip(this, Context.Tooltip(Item));
                return;
            }

            _shownChoices = [.. choices];
            _shownCurrent = current;
            _tiles.Children.Clear();

            var take = Form == RibbonForm.Popover ? choices.Count : Spec.InlineTiles;

            foreach (var choice in choices.Take(take))
                _tiles.Children.Add(Tile(choice, choice.Key == current));
        }
        else
        {
            var chosen = choices.FirstOrDefault(choice => choice.Key == current);

            _face.Content = Form == RibbonForm.Compact && Context.Icon(Item.Command.Id.Value) is { } icon
                ? icon
                : chosen?.Sample is { } sample
                    ? Sampled(chosen.Title, sample)
                    : new TextBlock
                {
                    Text = chosen?.Title ?? Item.Title,
                    FontSize = Context.Metrics.FontSize,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
        }

        ToolTip.SetTip(this, Context.Tooltip(Item));
    }

    private Control Panel()
    {
        if (Spec.Look == RibbonChoiceLook.Band)
            return new ChoiceGrid(Context).Build(Spec, Item.Title, Choose,
                Spec.MoreCommandId is { } next ? () =>
                {
                    _flyout?.Hide();
                    Context.Invoke(next);
                } : null);

        var choices = Spec.Choices();
        var current = Spec.Current();

        var layout = new StackPanel { Spacing = 2 };
        layout.Children.Add(Context.Caption(Item.Title));

        if (Spec.Look == RibbonChoiceLook.Gallery)
        {
            var grid = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 5 * (Math.Max(TileWidth, choices.Any(choice => choice.Sample is not null) ? SampleTileWidth : TileWidth) + 4) };

            foreach (var choice in choices) grid.Children.Add(Tile(choice, choice.Key == current));

            layout.Children.Add(grid);
        }
        else
        {
            foreach (var choice in choices) layout.Children.Add(Row(choice, choice.Key == current));
        }

        if (Spec.MoreCommandId is { } more)
        {
            layout.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 4) }
                .Themed(Border.BackgroundProperty, Palette.Keys.Border));

            var button = Context.Flat(new TextBlock
            {
                Text = Context.Texts["ribbon.choice.more"],
                FontSize = Context.Metrics.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
            });

            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
            button.Click += (_, _) =>
            {
                _flyout?.Hide();
                Context.Invoke(more);
            };

            layout.Children.Add(button);
        }

        return layout;
    }

    private Button Row(RibbonChoice choice, bool selected)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), MinWidth = 240 };

        if (Context.Icon(choice.IconKey) is { } icon)
        {
            icon.Margin = new Thickness(0, 0, Context.Metrics.SpacingSmall, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(icon);
        }

        var title = new TextBlock
        {
            Text = choice.Title,
            FontSize = Context.Metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        if (choice.Preview is { Length: > 0 } preview)
        {
            var sample = new TextBlock
            {
                Text = preview,
                FontSize = Context.Metrics.FontSizeSmall,
                Margin = new Thickness(Context.Metrics.SpacingMedium, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

            Grid.SetColumn(sample, 2);
            grid.Children.Add(sample);
        }

        var button = Context.Flat(grid, height: Context.ItemHeight + 4);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.Classes.Set("checked", selected);
        button.Click += (_, _) => Choose(choice);

        return button;
    }

    private Button Tile(RibbonChoice choice, bool selected)
    {
        Control body;

        var width = choice.Sample is null ? TileWidth : SampleTileWidth;
        var thumbnailWidth = TileWidth - 8;
        var thumbnailHeight = Math.Min(thumbnailWidth * 9 / 16, Context.ItemHeight * 2);

        if (Context.Thumbnail(choice.Thumbnail, thumbnailWidth, thumbnailHeight) is { } thumbnail)
        {
            thumbnail.HorizontalAlignment = HorizontalAlignment.Center;
            thumbnail.VerticalAlignment = VerticalAlignment.Center;
            body = thumbnail;
        }
        else if (choice.Sample is { } sample)
        {
            var text = Styled(sample, SampleText, Context.ItemHeight * 1.1 - 4);
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.VerticalAlignment = VerticalAlignment.Center;

            var swatch = new Border
            {
                Height = Context.ItemHeight * 1.1,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4, 0),
                ClipToBounds = true,
                Background = Backdrop(sample),
                BorderThickness = new Thickness(sample.BorderHex is null ? 0 : 1),
                BorderBrush = Brush(sample.BorderHex),
                Child = text,
            };

            var name = Label(choice.Title, width);
            name.TextWrapping = TextWrapping.Wrap;
            name.MaxLines = 2;

            body = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    swatch,
                    name,
                },
            };
        }
        else
        {
            var stack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };

            if (Context.Icon(choice.IconKey, Context.Metrics.IconSize * 1.6) is { } icon)
            {
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                stack.Children.Add(icon);
            }

            stack.Children.Add(Label(choice.Title, TileWidth));
            body = stack;
        }

        var button = Context.Flat(body, width: width, height: choice.Sample is null ? Context.ItemHeight * 2 + 8 : Context.ItemHeight * 2.4 + 16);
        button.Classes.Add("tile");
        button.Padding = new Thickness(3);
        button.Classes.Set("checked", selected);
        button.Click += (_, _) => Choose(choice);
        ToolTip.SetTip(button, choice.Preview is { } preview ? $"{choice.Title}\n{preview}" : choice.Title);

        return button;
    }

    private Control Sampled(string title, RibbonSample sample)
    {
        var text = Styled(sample, title, Context.ItemHeight - 6);
        text.FontSize = _scale.FitLabel(sample.FontSize, Context.Metrics.FontSize, Context.ItemHeight - 6);
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.HorizontalAlignment = HorizontalAlignment.Left;

        return new Border
        {
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 1),
            ClipToBounds = true,
            Background = Backdrop(sample),
            Child = text,
        };
    }

    private TextBlock Label(string text, double width) => new()
    {
        Text = text,
        FontSize = Context.Metrics.FontSizeSmall,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        MaxWidth = width - 8,
    };

    private TextBlock Styled(RibbonSample sample, string content, double available)
    {
        var text = new TextBlock
        {
            Text = content,
            FontSize = _scale.Fit(sample.FontSize, Context.Metrics.FontSize, available),
            FontWeight = sample.Bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = sample.Italic ? FontStyle.Italic : FontStyle.Normal,
            HorizontalAlignment = sample.Align switch
            {
                RibbonSampleAlign.Start => HorizontalAlignment.Left,
                RibbonSampleAlign.End => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            },
        };

        if (sample.FontFamily is { Length: > 0 } family) text.FontFamily = Context.FontFamily(family);

        var decorations = new TextDecorationCollection();
        if (sample.Underline) decorations.AddRange(TextDecorations.Underline);
        if (sample.Strikethrough) decorations.AddRange(TextDecorations.Strikethrough);
        if (decorations.Count > 0) text.TextDecorations = decorations;

        if (Brush(sample.TextHex) is { } ink) text.Foreground = ink;

        return text;
    }

    private void Choose(RibbonChoice choice)
    {
        _flyout?.Hide();
        Context.Invoke(Item.Command, choice.Key);
    }

    private const double LegibleContrast = 3.0;

    private IBrush Backdrop(RibbonSample sample)
    {
        if (Brush(sample.FillHex) is { } fill) return fill;
        if (sample.TextHex is null || !Color.TryParse(sample.TextHex, out var ink)) return Brushes.Transparent;
        var surface = (this.FindResource(Palette.Keys.SurfaceRaised) as ISolidColorBrush)?.Color ?? Colors.White;
        if (_contrast.Ratio(ink, surface) >= LegibleContrast) return Brushes.Transparent;
        return _contrast.Ratio(ink, Colors.White) >= _contrast.Ratio(ink, Colors.Black) ? Brushes.White : Brushes.Black;
    }

    private IBrush? Brush(string? hex) =>
        hex is not null && Color.TryParse(hex, out var colour) ? new SolidColorBrush(colour) : null;
}
