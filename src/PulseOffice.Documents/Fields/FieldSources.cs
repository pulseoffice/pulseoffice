using System.Globalization;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public interface IFieldValueSource
{
    IReadOnlyCollection<TextFieldKind> Kinds { get; }

    FieldValue? Evaluate(TextRun run, FieldContext context);
}

public sealed class PropertyFieldSource(ITextCultures cultures) : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } =
    [
        TextFieldKind.Title, TextFieldKind.Subject, TextFieldKind.Description, TextFieldKind.Keywords,
        TextFieldKind.Author, TextFieldKind.CreatedDate, TextFieldKind.ModifiedDate, TextFieldKind.FileName,
        TextFieldKind.CustomProperty,
    ];

    public FieldValue? Evaluate(TextRun run, FieldContext context)
    {
        if (run.FieldDetail?.Fixed == true) return null;
        var properties = context.Document.Properties;
        var text = run.Field switch
        {
            TextFieldKind.Title => properties.Title,
            TextFieldKind.Subject => properties.Subject,
            TextFieldKind.Description => properties.Description,
            TextFieldKind.Keywords => string.Join(", ", properties.Keywords),
            TextFieldKind.Author => properties.Author,
            TextFieldKind.CreatedDate => Date(properties.Created, run.FieldDetail?.Format, cultures.For(run.Language, context.Document.Language)),
            TextFieldKind.ModifiedDate => Date(properties.Modified, run.FieldDetail?.Format, cultures.For(run.Language, context.Document.Language)),
            TextFieldKind.FileName => context.Document.FileName,
            TextFieldKind.CustomProperty => run.FieldArgument is { } name && properties.Custom.TryGetValue(name, out var custom) ? custom : string.Empty,
            _ => null,
        };
        return text is null ? null : new FieldValue(text);
    }

    private string? Date(DateTimeOffset? value, string? format, CultureInfo culture)
    {
        if (value is not { } date) return null;
        try
        {
            return date.LocalDateTime.ToString(format is { Length: > 0 } custom ? custom : "d", culture);
        }
        catch (FormatException)
        {
            return date.LocalDateTime.ToString("d", culture);
        }
    }
}

public sealed class ClockFieldSource(ITextCultures cultures) : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.Date, TextFieldKind.Time];

    public FieldValue? Evaluate(TextRun run, FieldContext context)
    {
        if (run.FieldDetail?.Fixed == true) return null;
        var fallback = run.Field == TextFieldKind.Date ? "d" : "t";
        var format = run.FieldDetail?.Format is { Length: > 0 } custom ? custom : fallback;
        var culture = cultures.For(run.Language, context.Document.Language);
        try
        {
            return new FieldValue(context.Request.Now.ToString(format, culture));
        }
        catch (FormatException)
        {
            return new FieldValue(context.Request.Now.ToString(fallback, culture));
        }
    }
}

public sealed class StatisticFieldSource(IDocumentStatisticsCounter counter, IBlockTree tree, ITextCultures cultures) : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } =
    [
        TextFieldKind.WordCount, TextFieldKind.CharacterCount, TextFieldKind.ParagraphCount,
        TextFieldKind.TableCount, TextFieldKind.ImageCount, TextFieldKind.ObjectCount, TextFieldKind.PageCount,
    ];

    public FieldValue? Evaluate(TextRun run, FieldContext context)
    {
        var document = context.Document;
        var statistics = context.Statistics ??= counter.Count(tree.Paragraphs(document.Body).Select(paragraph => paragraph.Text));
        int? value = run.Field switch
        {
            TextFieldKind.WordCount => statistics.Words,
            TextFieldKind.CharacterCount => statistics.Characters,
            TextFieldKind.ParagraphCount => statistics.Paragraphs,
            TextFieldKind.TableCount => Objects(context).Tables,
            TextFieldKind.ImageCount => Objects(context).Images,
            TextFieldKind.ObjectCount => Objects(context).Objects,
            TextFieldKind.PageCount => context.Facts.PageCount,
            _ => null,
        };
        return value is { } number ? new FieldValue(number.ToString(cultures.For(run.Language, document.Language))) : null;
    }

    private FieldObjectCounts Objects(FieldContext context)
    {
        if (context.ObjectCounts is { } known) return known;
        var document = context.Document;
        var images = document.Objects.All.Count(item => item.Content is EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject });
        var embedded = document.Objects.All.Count(item => item.Content is EmbeddedContent);
        return context.ObjectCounts = new FieldObjectCounts(tree.Blocks(document.Body).OfType<TableBlock>().Count(), images, embedded - images);
    }
}

public sealed class VariableFieldSource(IFieldCondition conditions) : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.Variable, TextFieldKind.VariableSet];

    public FieldValue? Evaluate(TextRun run, FieldContext context)
    {
        if (run.FieldArgument is not { Length: > 0 } name) return null;
        if (run.Field == TextFieldKind.VariableSet)
        {
            var value = conditions.Value(run.FieldDetail?.Format ?? run.Text, context.Variables);
            context.Variables[name] = value;
            return new FieldValue(value, run.FieldDetail?.WhenFalse == FieldVisibility.Invisible ? true : null);
        }
        return new FieldValue(context.Variables.TryGetValue(name, out var current) ? current : string.Empty);
    }
}

public static class FieldVisibility
{
    public const string Invisible = "invisible";
}

public sealed class ConditionalFieldSource(IFieldCondition conditions) : IFieldValueSource
{
    public const string HiddenParagraphMark = "\u200B";

    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.ConditionalText, TextFieldKind.HiddenText, TextFieldKind.HiddenParagraph];

    public FieldValue Evaluate(TextRun run, FieldContext context)
    {
        var holds = conditions.Holds(run.FieldArgument, context.Variables);
        if (run.Field == TextFieldKind.HiddenParagraph) return new FieldValue(HiddenParagraphMark, holds);
        return run.Field == TextFieldKind.HiddenText
            ? new FieldValue(run.FieldDetail?.WhenTrue ?? run.Text, holds)
            : new FieldValue(holds ? run.FieldDetail?.WhenTrue ?? string.Empty : run.FieldDetail?.WhenFalse ?? string.Empty);
    }
}

public sealed class SequenceFieldSource : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.Sequence];

    public FieldValue Evaluate(TextRun run, FieldContext context)
    {
        var name = run.FieldArgument ?? string.Empty;
        var value = context.Sequences.GetValueOrDefault(name) + 1;
        context.Sequences[name] = value;
        return new FieldValue(value.ToString(CultureInfo.InvariantCulture));
    }
}

public sealed class ChapterFieldSource : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.Chapter];

    public FieldValue Evaluate(TextRun run, FieldContext context)
    {
        if (context.Chapter is not { } chapter) return new FieldValue(string.Empty);
        var label = context.Facts.ListLabel(chapter.Paragraph.Id)?.Trim() ?? string.Empty;
        return new FieldValue(run.FieldArgument switch
        {
            "number" => label,
            "number-and-name" => (label + " " + chapter.Text).Trim(),
            _ => chapter.Text,
        });
    }
}

public sealed class ReferenceFieldSource(IBookmarkCatalog bookmarks, IStoryCatalog stories, IDocumentOutline outline, IFieldWords words) : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.Bookmark, TextFieldKind.Reference];

    public FieldValue Evaluate(TextRun run, FieldContext context)
    {
        if (run.FieldArgument is not { Length: > 0 } name || bookmarks.Find(context.Document, name) is not { } bookmark)
            return new FieldValue(words.MissingReference);
        var target = stories.Find(context.Document, bookmark.ParagraphId).Paragraph;
        return new FieldValue((run.FieldDetail?.Format ?? ReferenceFormats.Text) switch
        {
            ReferenceFormats.Page => context.Facts.PageOf(bookmark.ParagraphId) ?? run.Text,
            ReferenceFormats.Number => Number(target, context) ?? run.Text,
            ReferenceFormats.Direction => context.OrderOf(bookmark.ParagraphId) < context.CurrentOrder
                || context.OrderOf(bookmark.ParagraphId) == context.CurrentOrder && bookmark.Offset < context.Offset ? words.Above : words.Below,
            ReferenceFormats.Chapter => Chapter(context, target) ?? run.Text,
            ReferenceFormats.Caption or ReferenceFormats.Category => Caption(target, false) ?? bookmarks.TextOf(context.Document, bookmark),
            ReferenceFormats.Value => Caption(target, true) ?? bookmarks.TextOf(context.Document, bookmark),
            _ => bookmarks.TextOf(context.Document, bookmark),
        });
    }

    private string? Number(ParagraphBlock target, FieldContext context) =>
        context.Facts.ListLabel(target.Id)?.Trim() is { Length: > 0 } label ? label : Caption(target, true);

    private string? Chapter(FieldContext context, ParagraphBlock target)
    {
        OutlineEntry? last = null;
        foreach (var heading in outline.Headings(context.Document, 1))
        {
            if (context.OrderOf(heading.Paragraph.Id) > context.OrderOf(target.Id)) break;
            last = heading;
        }
        return last is null ? null : context.Facts.ListLabel(last.Paragraph.Id)?.Trim() is { Length: > 0 } label ? label : last.Text;
    }

    private string? Caption(ParagraphBlock target, bool valueOnly)
    {
        var position = 0;
        foreach (var run in target.Content.Runs)
        {
            position += run.Text.Length;
            if (run.Field == TextFieldKind.Sequence) return valueOnly ? run.Text : target.Text[..position];
        }
        return null;
    }
}

public sealed class NoteReferenceFieldSource(IFieldWords words) : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.NoteReference];

    public FieldValue Evaluate(TextRun run, FieldContext context)
    {
        if (run.FieldArgument is not { Length: > 0 } id || context.Document.Notes.Find(id) is not { } note) return new FieldValue(words.MissingReference);
        if (context.Facts.NoteCitation(id) is { } citation) return new FieldValue(citation);
        if (note.Citation is { } custom) return new FieldValue(custom);
        var settings = note.Kind == Annotations.NoteKind.Footnote ? context.Document.Notes.FootnoteSettings : context.Document.Notes.EndnoteSettings;
        var position = context.Document.Notes.OfKind(note.Kind).ToList().FindIndex(item => item.Id == id);
        return new FieldValue((settings.StartAt + position).ToString(CultureInfo.InvariantCulture));
    }
}

public sealed class CitationFieldSource : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.Citation];

    public FieldValue? Evaluate(TextRun run, FieldContext context) =>
        run.FieldArgument is { Length: > 0 } identifier ? new FieldValue("[" + identifier + "]") : null;
}
