using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class ChoiceGrid(RibbonBuildContext context)
{
    public Control Build(ChoiceSpec spec, string title, Action<RibbonChoice> choose, Action? more)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(choose);

        var choices = spec.Choices();
        var current = spec.Current();
        var band = spec.Band;
        var tileWidth = band.PopoverTileWidth;
        var gap = context.Metrics.SpacingSmall;

        var layout = new StackPanel { Spacing = context.Metrics.SpacingSmall };
        var sections = Sections(choices);

        if (sections.Count == 1 && sections[0].Title is null) layout.Children.Add(context.Caption(title));

        foreach (var (section, entries) in sections)
        {
            if (section is not null) layout.Children.Add(context.Caption(section));

            var grid = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                ItemSpacing = gap,
                LineSpacing = gap,
                Width = band.PopoverColumns * (tileWidth + gap),
            };

            foreach (var choice in entries) grid.Children.Add(Tile(choice, choice.Key == current, tileWidth, band.CurrentAspect, choose));

            layout.Children.Add(grid);
        }

        if (more is not null)
        {
            layout.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 2) }
                .Themed(Border.BackgroundProperty, Palette.Keys.Border));

            var button = context.Flat(new TextBlock
            {
                Text = context.Texts["ribbon.choice.more"],
                FontSize = context.Metrics.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
            });

            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(context.Metrics.SpacingSmall, 0);
            button.Click += (_, _) => more();
            layout.Children.Add(button);
        }

        return layout;
    }

    private List<(string? Title, List<RibbonChoice> Entries)> Sections(IReadOnlyList<RibbonChoice> choices)
    {
        var sections = new List<(string?, List<RibbonChoice>)>();

        foreach (var choice in choices)
        {
            if (sections.Count == 0 || sections[^1].Item1 != choice.Section)
                sections.Add((choice.Section, []));

            sections[^1].Item2.Add(choice);
        }

        return sections;
    }

    private Button Tile(RibbonChoice choice, bool selected, double width, double aspect, Action<RibbonChoice> choose)
    {
        var thumbnailWidth = width - 10;
        var thumbnailHeight = thumbnailWidth / (aspect > 0 ? aspect : 1);

        var body = new StackPanel { Spacing = context.Metrics.SpacingSmall / 2 };

        if (context.Thumbnail(choice.Thumbnail, thumbnailWidth, thumbnailHeight) is { } thumbnail)
        {
            thumbnail.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(context.Metrics.Radius),
                ClipToBounds = true,
                Child = thumbnail,
            });
        }

        body.Children.Add(new TextBlock
        {
            Text = choice.Title,
            FontSize = context.Metrics.FontSizeSmall,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });

        var button = context.Flat(body, width: width, height: double.NaN);
        button.Classes.Add("tile");
        button.Padding = new Thickness(4);
        button.VerticalContentAlignment = VerticalAlignment.Top;
        button.Classes.Set("checked", selected);
        button.Click += (_, _) => choose(choice);
        ToolTip.SetTip(button, choice.Preview is { } preview ? $"{choice.Title}\n{preview}" : choice.Title);

        return button;
    }
}
