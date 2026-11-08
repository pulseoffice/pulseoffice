using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Objects;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Accessibility;

public enum AccessibilityIssueKind
{
    MissingTitle,
    MissingLanguage,
    MissingAltText,
    SkippedHeadingLevel,
    EmptyParagraphs,
    TableWithoutHeader,
    LowContrast,
    LinkShowsAddress,
}

public sealed record AccessibilityIssue(AccessibilityIssueKind Kind, string? ParagraphId = null, string? ObjectId = null, string? Detail = null);

public interface IAccessibilityRule
{
    IEnumerable<AccessibilityIssue> Check(TextDocument document);
}

public sealed class DocumentMetadataRule : IAccessibilityRule
{
    public IEnumerable<AccessibilityIssue> Check(TextDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Properties.Title)) yield return new AccessibilityIssue(AccessibilityIssueKind.MissingTitle);
        if (string.IsNullOrWhiteSpace(document.Language)) yield return new AccessibilityIssue(AccessibilityIssueKind.MissingLanguage);
    }
}

public sealed class AlternativeTextRule : IAccessibilityRule
{
    public IEnumerable<AccessibilityIssue> Check(TextDocument document) =>
        document.Objects.All
            .Where(item => item.Content is EmbeddedContent { Value: var value } && value.Kind != PulseOffice.Document.Embedding.EmbeddedObjectKind.FormControl
                && string.IsNullOrWhiteSpace(value.Description) && string.IsNullOrWhiteSpace(value.Title))
            .Select(item => new AccessibilityIssue(AccessibilityIssueKind.MissingAltText, ObjectId: item.Id, Detail: item.Name ?? item.Id));
}

public sealed class HeadingOrderRule(IDocumentOutline outline) : IAccessibilityRule
{
    public IEnumerable<AccessibilityIssue> Check(TextDocument document)
    {
        var previous = 0;
        foreach (var heading in outline.Headings(document))
        {
            if (heading.Level > previous + 1)
                yield return new AccessibilityIssue(AccessibilityIssueKind.SkippedHeadingLevel, heading.Paragraph.Id, Detail: heading.Text);
            previous = heading.Level;
        }
    }
}

public sealed class SpacingParagraphsRule(IBlockTree tree) : IAccessibilityRule
{
    public IEnumerable<AccessibilityIssue> Check(TextDocument document)
    {
        var run = 0;
        foreach (var paragraph in tree.Paragraphs(document.Body))
        {
            run = paragraph.Text.Trim().Length == 0 ? run + 1 : 0;
            if (run == 2) yield return new AccessibilityIssue(AccessibilityIssueKind.EmptyParagraphs, paragraph.Id);
        }
    }
}

public sealed class TableHeaderRule(IBlockTree tree) : IAccessibilityRule
{
    public IEnumerable<AccessibilityIssue> Check(TextDocument document) =>
        tree.Blocks(document.Body).OfType<TableBlock>()
            .Where(table => table.HeaderRows == 0 && table.Rows.Count > 1)
            .Select(table => new AccessibilityIssue(AccessibilityIssueKind.TableWithoutHeader,
                table.Rows[0].Cells.FirstOrDefault()?.Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault()?.Id, Detail: table.Name));
}

public sealed class ContrastRule(IBlockTree tree) : IAccessibilityRule
{
    private const double Minimum = 4.5;

    public IEnumerable<AccessibilityIssue> Check(TextDocument document) =>
        tree.Paragraphs(document.Body)
            .Where(paragraph => paragraph.Content.Runs.Any(run => run.Text.Trim().Length > 0 && run.Color is { } color && Ratio(color, run.Highlight ?? paragraph.Content.Background ?? 0xFFFFFFFF) < Minimum))
            .Select(paragraph => new AccessibilityIssue(AccessibilityIssueKind.LowContrast, paragraph.Id, Detail: paragraph.Text));

    private double Ratio(uint ink, uint paper)
    {
        var a = Luminance(ink);
        var b = Luminance(paper >> 24 == 0 ? 0xFFFFFFFF : paper);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private double Luminance(uint argb)
    {
        double Channel(uint value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel((argb >> 16) & 0xFF) + 0.7152 * Channel((argb >> 8) & 0xFF) + 0.0722 * Channel(argb & 0xFF);
    }
}

public sealed class LinkTextRule(IBlockTree tree) : IAccessibilityRule
{
    public IEnumerable<AccessibilityIssue> Check(TextDocument document) =>
        tree.Paragraphs(document.Body)
            .Where(paragraph => paragraph.Content.Runs.Any(run => run.Hyperlink is { Length: > 0 } link && string.Equals(run.Text.Trim(), link, StringComparison.OrdinalIgnoreCase)))
            .Select(paragraph => new AccessibilityIssue(AccessibilityIssueKind.LinkShowsAddress, paragraph.Id, Detail: paragraph.Text));
}

public interface IAccessibilityAudit
{
    IReadOnlyList<AccessibilityIssue> Check(TextDocument document);
}

public sealed class AccessibilityAudit(IEnumerable<IAccessibilityRule> rules) : IAccessibilityAudit
{
    public IReadOnlyList<AccessibilityIssue> Check(TextDocument document) => [.. rules.SelectMany(rule => rule.Check(document))];
}
