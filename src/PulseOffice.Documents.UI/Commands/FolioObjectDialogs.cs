using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.Documents.UI.Commands;

public enum ObjectDialogPage
{
    Position,
    Wrap,
    Options,
}

public sealed record ObjectPropertiesChoice(
    ObjectAnchorKind Anchor,
    DocumentSize Size,
    ObjectPlacement Placement,
    double Rotation,
    WrapMode Wrap,
    bool Contour,
    bool FirstParagraph,
    bool Background,
    Margins Spacing,
    string? Name,
    string? Title,
    string? Description);

public interface IFolioObjectDialogs
{
    Task<ObjectPropertiesChoice?> PropertiesAsync(AnchoredObject item, ObjectAnchorKind anchor, ObjectDialogPage page, CancellationToken ct);

    Task<string?> NameAsync(string? current, CancellationToken ct);

    Task<CaptionRequest?> CaptionAsync(string category, CancellationToken ct);

    Task<string?> LinkAsync(IReadOnlyList<AnchoredObject> targets, CancellationToken ct);
}

public sealed class FolioObjectDialogs(IFolioForms forms, ITabbedForms tabbed, ITextCatalog texts, ICaptions captions, IDialogService dialogs, IIconCatalog? icons = null) : IFolioObjectDialogs
{
    private readonly ObjectAnchorKind[] _anchors = [ObjectAnchorKind.Page, ObjectAnchorKind.Paragraph, ObjectAnchorKind.Character, ObjectAnchorKind.AsCharacter];
    private readonly WrapMode[] _wraps = [WrapMode.None, WrapMode.Left, WrapMode.Right, WrapMode.Parallel, WrapMode.Through, WrapMode.Optimal];
    private readonly HorizontalRelation[] _horizontal = [HorizontalRelation.Paragraph, HorizontalRelation.PageArea, HorizontalRelation.Page, HorizontalRelation.Margin, HorizontalRelation.Character];
    private readonly VerticalRelation[] _vertical = [VerticalRelation.Paragraph, VerticalRelation.PageArea, VerticalRelation.Page, VerticalRelation.Margin, VerticalRelation.Line];

    public async Task<ObjectPropertiesChoice?> PropertiesAsync(AnchoredObject item, ObjectAnchorKind anchor, ObjectDialogPage page, CancellationToken ct)
    {
        var embedded = (item.Content as EmbeddedContent)?.Value;
        var spacing = item.WrapSpacing;
        var wrap = item.Wrap == WrapMode.TopAndBottom ? WrapMode.None : item.Wrap;
        var position = new List<FormField>
        {
            new SizeLockField("size", texts["size.title"], new LockedSize(Centimetres(item.Size.Width), Centimetres(item.Size.Height), embedded is not null), "cm", 0.05, 500, 0.1, icons),
            new IconChoiceField("anchor", texts["folio.object.anchor"],
                [.. _anchors.Select(kind => new IconChoice(Key(kind), $"folio.object.anchor.{Key(kind)}", texts[$"command.folio.object.anchor.{Key(kind)}.title"]))], icons, Math.Max(0, Array.IndexOf(_anchors, anchor))),
            new NumberField("x", texts["folio.object.x"], Centimetres(item.Placement.X), -500, 500, 0.1) { Unit = "cm" },
            new ChoiceField("horizontal", texts["folio.object.horizontal"], [.. _horizontal.Select(relation => texts[$"folio.object.relation.{Key(relation)}"])], Math.Max(0, Array.IndexOf(_horizontal, item.Placement.Horizontal))),
            new NumberField("y", texts["folio.object.y"], Centimetres(item.Placement.Y), -500, 500, 0.1) { Unit = "cm" },
            new ChoiceField("vertical", texts["folio.object.vertical"], [.. _vertical.Select(relation => texts[$"folio.object.relation.{Key(relation)}"])], Math.Max(0, Array.IndexOf(_vertical, item.Placement.Vertical))),
            new NumberField("rotation", texts["folio.object.rotation"], Math.Round(item.RotationDegrees, 2), 0, 359.99, 1) { Unit = "°" },
        };
        var wrapping = new List<FormField>
        {
            new IconChoiceField("wrap", texts["folio.object.wrap"],
                [.. _wraps.Select(mode => new IconChoice(Key(mode), $"folio.object.wrap.{Key(mode)}", texts[$"command.folio.object.wrap.{Key(mode)}.title"]))], icons, Math.Max(0, Array.IndexOf(_wraps, wrap))),
            new CheckField("firstParagraph", texts["command.folio.object.wrap.firstParagraph.title"], item.WrapFirstParagraph),
            new CheckField("background", texts["command.folio.object.wrap.background.title"], item.InBackground),
            new NumberField("left", texts["folio.object.spacing.left"], Points(spacing.Left), 0, 200, 1) { Unit = "pt" },
            new NumberField("right", texts["folio.object.spacing.right"], Points(spacing.Right), 0, 200, 1) { Unit = "pt" },
            new NumberField("top", texts["folio.object.spacing.top"], Points(spacing.Top), 0, 200, 1) { Unit = "pt" },
            new NumberField("bottom", texts["folio.object.spacing.bottom"], Points(spacing.Bottom), 0, 200, 1) { Unit = "pt" },
        };
        if (embedded is not null) wrapping.Insert(1, new CheckField("contour", texts["command.folio.object.wrap.contour.title"], item.WrapContour));
        var options = new List<FormField> { new TextField("name", texts["folio.object.name"], item.Name ?? string.Empty) };
        if (embedded is not null)
        {
            options.Add(new TextField("title", texts["folio.object.title"], embedded.Title ?? string.Empty));
            options.Add(new TextField("description", texts["folio.object.description"], embedded.Description ?? string.Empty) { Multiline = true });
        }
        FormTab[] tabs =
        [
            new(texts["folio.object.tab.position"], texts["folio.object.position.explanation"], position),
            new(texts["folio.object.tab.wrap"], texts["folio.object.wrap.explanation"], wrapping),
            new(texts["folio.object.tab.options"], texts["folio.object.options.explanation"], options),
        ];
        if (await tabbed.AskAsync("command.folio.object.properties.title", tabs, ct, (int)page, 560).ConfigureAwait(true) is not { } values) return null;
        return new ObjectPropertiesChoice(
            _anchors[values.Choice("anchor", _anchors.Length)],
            values.TryGetValue("size", out var sized) && sized is LockedSize size ? new DocumentSize(Twip.FromCentimeters(size.Width), Twip.FromCentimeters(size.Height)) : item.Size,
            new ObjectPlacement(Twip.FromCentimeters(values.Number("x", Centimetres(item.Placement.X))), Twip.FromCentimeters(values.Number("y", Centimetres(item.Placement.Y))))
            {
                Horizontal = _horizontal[values.Choice("horizontal", _horizontal.Length)],
                Vertical = _vertical[values.Choice("vertical", _vertical.Length)],
            },
            values.Number("rotation", item.RotationDegrees),
            _wraps[values.Choice("wrap", _wraps.Length)] is var chosenWrap && chosenWrap == wrap ? item.Wrap : chosenWrap,
            values.Flag("contour"),
            values.Flag("firstParagraph"),
            values.Flag("background"),
            new Margins(Twip.FromPoints(values.Number("left", 0)), Twip.FromPoints(values.Number("top", 0)), Twip.FromPoints(values.Number("right", 0)), Twip.FromPoints(values.Number("bottom", 0))),
            Optional(values.Text("name")),
            embedded is null ? null : Optional(values.Text("title")),
            embedded is null ? null : Optional(values.Text("description")));
    }

    public async Task<string?> NameAsync(string? current, CancellationToken ct)
    {
        FormField[] fields = [new TextField("name", texts["folio.object.name"], current ?? string.Empty)];
        if (await forms.AskAsync("command.folio.object.name.title", "folio.object.name.explanation", fields, ct).ConfigureAwait(true) is not { } values) return null;
        return values.Text("name").Trim();
    }

    public async Task<CaptionRequest?> CaptionAsync(string category, CancellationToken ct)
    {
        var categories = captions.Categories;
        FormField[] fields =
        [
            new ChoiceField("category", texts["folio.caption.category"], [.. categories.Select(name => texts[$"folio.caption.category.{name.ToLowerInvariant()}"])], Math.Max(0, categories.ToList().IndexOf(category))),
            new TextField("text", texts["folio.caption.text"]),
            new ChoiceField("position", texts["folio.caption.position"], [texts["folio.caption.below"], texts["folio.caption.above"]], category == "Table" ? 1 : 0),
        ];
        var view = new FormPromptView(texts["folio.caption.explanation"], fields, texts);
        var sample = new TextBlock { Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap, FontStyle = FontStyle.Italic, MaxWidth = 220 };
        var picture = new Border { Width = 120, Height = 64, Background = new SolidColorBrush(Color.FromRgb(0xDB, 0xE4, 0xF0)), HorizontalAlignment = HorizontalAlignment.Center };
        var stack = new StackPanel { Spacing = 6, Margin = new Thickness(12) };
        void Refresh()
        {
            var values = view.Values;
            var chosen = categories[values.Choice("category", categories.Count)];
            var label = texts[$"folio.caption.category.{chosen.ToLowerInvariant()}"] + " 1";
            sample.Text = values.Text("text").Trim() is { Length: > 0 } text ? texts.Format("folio.caption.sample", label, text) : label;
            stack.Children.Clear();
            if (values.Choice("position", 2) == 1) stack.Children.Add(sample);
            stack.Children.Add(picture);
            if (values.Choice("position", 2) == 0) stack.Children.Add(sample);
        }
        Refresh();
        view.ValuesChanged += (_, _) => Refresh();
        view.Preview = new Border { Background = Brushes.White, Child = stack, MinWidth = 200 };
        if (await dialogs.ShowAsync<Dictionary<string, object>>(view, new DialogOptions { TitleKey = "command.folio.object.caption.title" }, ct).ConfigureAwait(true) is not { } values) return null;
        var chosen = categories[values.Choice("category", categories.Count)];
        return new CaptionRequest(chosen, texts[$"folio.caption.category.{chosen.ToLowerInvariant()}"], values.Text("text").Trim(), values.Choice("position", 2) == 1);
    }

    public async Task<string?> LinkAsync(IReadOnlyList<AnchoredObject> targets, CancellationToken ct)
    {
        if (targets.Count == 0) return null;
        FormField[] fields =
        [
            new ChoiceField("target", texts["folio.object.link.target"], [.. targets.Select((target, index) => target.Name ?? texts.Format("folio.object.link.unnamed", index + 1))]),
        ];
        if (await forms.AskAsync("command.folio.object.link.title", "folio.object.link.explanation", fields, ct).ConfigureAwait(true) is not { } values) return null;
        return targets[values.Choice("target", targets.Count)].Id;
    }

    public string Key(ObjectAnchorKind kind) => kind switch
    {
        ObjectAnchorKind.Page => "page",
        ObjectAnchorKind.Character => "character",
        ObjectAnchorKind.AsCharacter => "asCharacter",
        _ => "paragraph",
    };

    public string Key(WrapMode mode) => mode switch
    {
        WrapMode.Left => "left",
        WrapMode.Right => "right",
        WrapMode.Parallel => "parallel",
        WrapMode.Through => "through",
        WrapMode.Optimal => "optimal",
        _ => "none",
    };

    private string Key(HorizontalRelation relation) => relation switch
    {
        HorizontalRelation.PageArea => "pageArea",
        HorizontalRelation.Page => "page",
        HorizontalRelation.Margin => "margin",
        HorizontalRelation.Character => "character",
        _ => "paragraph",
    };

    private string Key(VerticalRelation relation) => relation switch
    {
        VerticalRelation.PageArea => "pageArea",
        VerticalRelation.Page => "page",
        VerticalRelation.Margin => "margin",
        VerticalRelation.Line => "line",
        _ => "paragraph",
    };

    private double Centimetres(Twip value) => Math.Round(value.Value / 1440.0 * 2.54, 2);

    private double Points(Twip value) => Math.Round(value.ToPoints(), 1);

    private string? Optional(string text) => text.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
