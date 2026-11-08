using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Shell;

public sealed class StartView : UserControl
{
    private const int RecentShown = 8;

    private readonly IRecentDocuments _recent;
    private readonly ITextCatalog _texts;
    private readonly IIconCatalog _icons;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly Grid _recentList = new();

    public event EventHandler<string>? CommandRequested;

    public StartView(IRecentDocuments recent, ITextCatalog texts, IIconCatalog icons)
    {
        _recent = recent ?? throw new ArgumentNullException(nameof(recent));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));

        Rebuild();
    }

    private void OnRecentChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(FillRecent);

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _recent.Changed -= OnRecentChanged;
        _recent.Changed += OnRecentChanged;
        ActualThemeVariantChanged -= OnThemeChanged;
        ActualThemeVariantChanged += OnThemeChanged;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _recent.Changed -= OnRecentChanged;
        ActualThemeVariantChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        Content = Build();
        FillRecent();
    }

    private bool Dark => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;

    private IBrush TileBackground(Color accent) => new SolidColorBrush(accent, Dark ? 0.24 : 0.12);

    private IBrush TileGlyph(Color accent) => new SolidColorBrush(Dark ? Lighter(accent) : accent);

    private Color Lighter(Color accent) => Color.FromRgb(
        (byte)(accent.R + (255 - accent.R) * 0.45),
        (byte)(accent.G + (255 - accent.G) * 0.45),
        (byte)(accent.B + (255 - accent.B) * 0.45));

    private Control Build()
    {
        var page = new StackPanel
        {
            MaxWidth = 820,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(_metrics.SpacingLarge * 2, _metrics.SpacingLarge * 2),
            Spacing = _metrics.SpacingLarge * 1.4,
        };

        page.Children.Add(Heading());
        page.Children.Add(SearchBox());
        page.Children.Add(Modules());
        page.Children.Add(Recent());

        var scroll = new ScrollViewer
        {
            Content = page,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        return new Panel { Children = { Backdrop(), scroll } };
    }

    private Control Backdrop()
    {
        var wash = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(16, 37, 99, 235), 0),
                    new GradientStop(Color.FromArgb(0, 37, 99, 235), 0.55),
                },
            },
        };

        var glow = new Panel();

        for (var index = 0; index < ModuleIdentity.All.Count; index++)
        {
            var identity = ModuleIdentity.All[index];

            glow.Children.Add(new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 520,
                Height = 520,
                Opacity = 0.04,
                Fill = new SolidColorBrush(Color.Parse(identity.AccentHex)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness((index - 1) * 460, 0, 0, -340),
                IsHitTestVisible = false,
            });
        }

        return new Panel { Children = { wash, glow }, IsHitTestVisible = false };
    }

    private Control Heading()
    {
        var logo = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(14),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#3730A3"), 0),
                    new GradientStop(Color.Parse("#1E1B4B"), 1),
                },
            },
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M3 13 H8 L10.5 7 L14 19 L16.5 13 H21"),
                Stroke = new SolidColorBrush(Color.Parse("#C7D2FE")),
                StrokeThickness = 2.2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Width = 24,
                Height = 24,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var lines = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

        lines.Children.Add(new TextBlock
        {
            Text = _texts[GreetingKey(DateTime.Now.Hour)],
            FontSize = _metrics.FontSizeDisplay - 4,
            FontWeight = FontWeight.SemiBold,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        lines.Children.Add(new TextBlock
        {
            Text = _texts["welcome.subtitle"],
            FontSize = _metrics.FontSize + 1,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingMedium + 2,
            Children = { logo, lines },
        };
    }

    private string GreetingKey(int hour) => hour switch
    {
        >= 5 and < 13 => "start.greeting.morning",
        >= 13 and < 18 => "start.greeting.afternoon",
        _ => "start.greeting.evening",
    };

    private Control SearchBox()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = _metrics.SpacingMedium };

        if (_icons.Find("shell.view.commandPalette") is { } search)
            row.Children.Add(new IconView(search, _metrics.IconSize + 2)
            {
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary));

        var prompt = new TextBlock
        {
            Text = _texts["start.search"],
            FontSize = _metrics.FontSize + 1,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(prompt, 1);
        row.Children.Add(prompt);

        var chip = new Border
        {
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = ShortcutGesture.Parse("Ctrl+Shift+P")?.Display() ?? string.Empty,
                FontSize = _metrics.FontSizeSmall,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        Grid.SetColumn(chip, 2);
        row.Children.Add(chip);

        var button = new Button
        {
            Content = row,
            Padding = new Thickness(_metrics.SpacingMedium + 2, _metrics.SpacingMedium - 1),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.Ibeam),
        };
        button.Classes.Add("startSearch");
        button.Themed(BackgroundProperty, Palette.Keys.Surface);
        button.Themed(BorderBrushProperty, Palette.Keys.Border);
        Avalonia.Automation.AutomationProperties.SetName(button, _texts["start.search"]);
        button.Click += (_, _) => CommandRequested?.Invoke(this, "shell.view.commandPalette");

        return button;
    }

    private Control Modules()
    {
        var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = _metrics.SpacingMedium + 2 };

        for (var index = 0; index < ModuleIdentity.All.Count; index++)
        {
            var card = ModuleCard(ModuleIdentity.All[index]);
            Grid.SetColumn(card, index);
            cards.Children.Add(card);
        }

        return cards;
    }

    private Control Recent()
    {
        var section = new StackPanel { Spacing = _metrics.SpacingSmall + 2 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var caption = new TextBlock
        {
            Text = _texts["start.recent"],
            FontSize = _metrics.FontSize + 1,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        var open = Quiet(_texts["start.open"], "shell.file.open", "shell.file.open");
        open.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(open, 1);

        header.Children.Add(caption);
        header.Children.Add(open);

        if (_recentList.Parent is Border previous) previous.Child = null;

        var list = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = _recentList,
        }
            .Themed(Border.BackgroundProperty, Palette.Keys.Surface)
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        section.Children.Add(header);
        section.Children.Add(list);

        return section;
    }

    private void FillRecent()
    {
        _recentList.Children.Clear();
        _recentList.RowDefinitions.Clear();

        var items = _recent.Items.Take(RecentShown).ToList();

        if (items.Count == 0)
        {
            _recentList.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _recentList.Children.Add(EmptyRecent());
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            _recentList.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var entry = RecentEntry(items[index], index < items.Count - 1);
            Grid.SetRow(entry, index);
            _recentList.Children.Add(entry);
        }
    }

    private Control EmptyRecent()
    {
        var content = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(_metrics.SpacingLarge, _metrics.SpacingLarge + 6),
        };

        if (_icons.Find("shell.file.open") is { } folder)
            content.Children.Add(new IconView(folder, _metrics.IconSize * 1.6)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
            }.Themed(IconView.ForegroundProperty, Palette.Keys.TextDisabled));

        content.Children.Add(new TextBlock
        {
            Text = _texts["start.emptyTitle"],
            FontSize = _metrics.FontSize + 1,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        content.Children.Add(new TextBlock
        {
            Text = _texts["start.noRecent"],
            FontSize = _metrics.FontSize,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        return content;
    }

    private Control RecentEntry(RecentDocument document, bool divided)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = _metrics.SpacingMedium,
        };

        var identity = ModuleIdentity.FromExtension(Path.GetExtension(document.Path)) ?? ModuleIdentity.Sheets;
        var accent = Color.Parse(identity.AccentHex);

        if (_icons.Find($"module.{identity.Kind.ToString().ToLowerInvariant()}") is { } geometry)
            row.Children.Add(new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = TileBackground(accent),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new IconView(geometry, _metrics.IconSize)
                {
                    Foreground = TileGlyph(accent),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };

        lines.Children.Add(new TextBlock
        {
            Text = document.Name,
            FontSize = _metrics.FontSize,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        var folder = new TextBlock
        {
            Text = Folder(document),
            FontSize = _metrics.FontSizeSmall,
            TextTrimming = TextTrimming.LeadingCharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        ToolTip.SetTip(folder, document.Path);
        lines.Children.Add(folder);

        Grid.SetColumn(lines, 1);
        row.Children.Add(lines);

        var opened = new TextBlock
        {
            Text = Since(document.LastOpened.LocalDateTime),
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(opened, 2);
        row.Children.Add(opened);

        var button = new Button
        {
            Content = row,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = new Thickness(_metrics.SpacingMedium + 2, _metrics.SpacingSmall + 2),
            CornerRadius = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        button.Classes.Add("startRecent");

        Avalonia.Automation.AutomationProperties.SetName(button, document.Name);
        button.Click += (_, _) => OpenRecent(document);

        if (!divided) return button;

        var divider = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);
        return new Panel { Children = { button, divider } };
    }

    private void OpenRecent(RecentDocument document)
    {
        var items = _recent.Items;
        for (var index = 0; index < items.Count; index++)
        {
            if (!string.Equals(items[index].Path, document.Path, StringComparison.Ordinal)) continue;

            CommandRequested?.Invoke(this, $"shell.file.recent.{index}");
            return;
        }
    }

    private string Since(DateTime opened)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var days = (DateTime.Today - opened.Date).Days;
        return days switch
        {
            0 => opened.ToString("t", culture),
            1 => _texts["start.yesterday"],
            > 1 and < 7 => culture.DateTimeFormat.GetDayName(opened.DayOfWeek),
            _ => opened.ToString("d", culture),
        };
    }

    private string Folder(RecentDocument document)
    {
        var folder = Path.GetDirectoryName(document.Path) ?? string.Empty;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var trimmedHome = home.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedHome.Length > 0 && folder.StartsWith(trimmedHome, StringComparison.OrdinalIgnoreCase)
            && (folder.Length == trimmedHome.Length || folder[trimmedHome.Length] == Path.DirectorySeparatorChar || folder[trimmedHome.Length] == Path.AltDirectorySeparatorChar))
            folder = "~" + folder[trimmedHome.Length..];

        return folder;
    }

    private Button Quiet(string label, string iconKey, string commandId)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
        };

        if (_icons.Find(iconKey) is { } geometry)
            content.Children.Add(new IconView(geometry, _metrics.IconSize)
            {
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(IconView.ForegroundProperty, Palette.Keys.TextSecondary));

        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = _metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = new Thickness(_metrics.SpacingSmall, _metrics.SpacingSmall / 2),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        button.Classes.Add("startQuiet");
        button.Click += (_, _) => CommandRequested?.Invoke(this, commandId);

        return button;
    }

    private Control ModuleCard(ModuleIdentity identity)
    {
        var accentColor = Color.Parse(identity.AccentHex);
        var accent = new SolidColorBrush(accentColor);
        var module = identity.Kind.ToString().ToLowerInvariant();

        var content = new StackPanel { Spacing = 2 };

        if (_icons.Find($"module.{module}") is { } geometry)
            content.Children.Add(new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(13),
                Background = TileBackground(accentColor),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, _metrics.SpacingMedium),
                Child = new IconView(geometry, _metrics.IconSize * 1.5)
                {
                    Foreground = TileGlyph(accentColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });

        content.Children.Add(new TextBlock
        {
            Text = identity.BrandName,
            FontSize = _metrics.FontSizeHeading + 1,
            FontWeight = FontWeight.SemiBold,
            Foreground = TileGlyph(accentColor),
        });

        content.Children.Add(new TextBlock
        {
            Text = _texts[$"start.tagline.{module}"],
            FontSize = _metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            Margin = new Thickness(0, _metrics.SpacingMedium + 2, 0, 0),
        };

        var create = new Button
        {
            Content = new Border
            {
                Background = accent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(_metrics.SpacingMedium + 4, _metrics.SpacingSmall),
                Child = new TextBlock { Text = _texts["start.create"], FontSize = _metrics.FontSize, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White },
            },
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = default,
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        create.Classes.Add("startCreate");
        Avalonia.Automation.AutomationProperties.SetName(create, $"{_texts["start.create"]} {identity.BrandName}");
        create.Click += (_, _) => CommandRequested?.Invoke(this, $"shell.file.new.{module}");

        var templates = new Button
        {
            Content = new TextBlock { Text = _texts["start.templatesShort"], FontSize = _metrics.FontSize }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(8),
            Cursor = new Cursor(StandardCursorType.Hand),
        }.Themed(BorderBrushProperty, Palette.Keys.Border);
        Avalonia.Automation.AutomationProperties.SetName(templates, $"{_texts["start.templatesShort"]} {identity.BrandName}");
        templates.Click += (_, _) => CommandRequested?.Invoke(this, $"shell.file.templates.{module}");

        actions.Children.Add(create);
        actions.Children.Add(templates);
        content.Children.Add(actions);

        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(_metrics.SpacingLarge),
            Child = content,
            RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(0px)"),
            Transitions =
            [
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(140) },
                new BoxShadowsTransition { Property = Border.BoxShadowProperty, Duration = TimeSpan.FromMilliseconds(140) },
            ],
        }
            .Themed(Border.BackgroundProperty, Palette.Keys.Surface)
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        card.PointerEntered += (_, _) =>
        {
            card.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(-2px)");
            card.BoxShadow = BoxShadows.Parse($"0 10 24 0 {Shadow(accentColor)}");
        };
        card.PointerExited += (_, _) =>
        {
            card.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(0px)");
            card.BoxShadow = default;
        };

        return card;
    }

    private string Shadow(Color accent) => $"#{0x22:X2}{accent.R:X2}{accent.G:X2}{accent.B:X2}";
}
