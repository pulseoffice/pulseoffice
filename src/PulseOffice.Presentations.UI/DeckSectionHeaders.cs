using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI;

public sealed class DeckSectionHeaders(DeckActions actions, ITextCatalog texts, DesignTokens.Metrics metrics)
{
    private sealed record Entry(SlideSectionHeader Header, Control Control);

    private Dictionary<SlideSectionHeader, Entry> _entries = [];
    private Dictionary<SlideSectionHeader, Entry> _next = [];

    public void Begin() => _next = [];

    public Control For(SlideSectionHeader header)
    {
        var entry = _entries.TryGetValue(header, out var kept) ? kept : new Entry(header, Build(header));
        _next[header] = entry;
        return entry.Control;
    }

    public void End() => _entries = _next;

    private Control Build(SlideSectionHeader section)
    {
        var label = new TextBlock
        {
            Text = section.Name.ToUpperInvariant(),
            FontSize = Math.Max(9, metrics.FontSizeSmall - 1),
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, section.IsEmpty ? Palette.Keys.TextDisabled : Palette.Keys.TextSecondary);

        var header = new Border
        {
            Child = label,
            Padding = new Thickness(metrics.SpacingSmall, metrics.SpacingSmall),
            Margin = new Thickness(0, metrics.SpacingSmall, 0, 0),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);
        Avalonia.Automation.AutomationProperties.SetName(header, section.Name);

        header.DoubleTapped += (_, _) => Rename(section, header, label);
        header.ContextMenu = Menu(section, header, label);
        return header;
    }

    private ContextMenu Menu(SlideSectionHeader section, Border header, TextBlock label)
    {
        var rename = new MenuItem { Header = texts["command.deck.section.rename.title"] };
        rename.Click += (_, _) => Rename(section, header, label);
        var remove = new MenuItem { Header = texts["command.deck.section.remove.title"] };
        remove.Click += (_, _) => Remove(section);
        var menu = new ContextMenu();
        menu.Items.Add(rename);
        menu.Items.Add(remove);
        return menu;
    }

    private void Remove(SlideSectionHeader section)
    {
        if (section.Empty is { } empty) actions.RemoveEmptySection(empty);
        else if (section.StartSlide is { } slide) actions.SetSection(slide, null);
    }

    private void Commit(SlideSectionHeader section, string name)
    {
        if (section.Empty is { } empty) actions.RenameEmptySection(empty, name);
        else if (section.StartSlide is { } slide) actions.SetSection(slide, name);
    }

    private void Rename(SlideSectionHeader section, Border header, TextBlock label)
    {
        var box = new TextBox
        {
            Text = section.Name,
            FontSize = metrics.FontSizeSmall,
            Padding = new Thickness(metrics.SpacingSmall, 2),
        };

        var finished = false;

        void Finish(bool commit)
        {
            if (finished) return;
            finished = true;
            var name = box.Text?.Trim();
            header.Child = label;
            if (commit && !string.IsNullOrEmpty(name) && name != section.Name) Commit(section, name);
        }

        box.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) { Finish(true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        box.LostFocus += (_, _) => Finish(true);

        header.Child = box;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { box.Focus(); box.SelectAll(); });
    }
}
