using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests.Odt;

internal sealed class TextDocumentEssence(bool collapseWhitespace = false)
{
    private readonly StyleResolver _resolver = new();
    private StyleSheet _styles = new();

    public string Describe(TextDocument document)
    {
        var text = new StringBuilder();
        _styles = document.Styles;
        Blocks(text, document.Body, string.Empty);
        foreach (var note in document.Notes.All.OrderBy(note => note.Kind).ThenBy(note => Plain(note.Content), StringComparer.Ordinal))
            text.AppendLine($"note {note.Kind} [{Plain(note.Content)}]");
        foreach (var line in document.Objects.All.Select(Object).Order(StringComparer.Ordinal)) text.AppendLine(line);
        return text.ToString();
    }

    private string Object(AnchoredObject item) => item.Content switch
    {
        TextBoxContent box => $"text object {Math.Round(item.Size.Width.Value / 20.0)} [{Plain(box.Content)}]",
        EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject, Story: { } story } when Plain(story).Trim('|').Length > 0 => $"text object {Math.Round(item.Size.Width.Value / 20.0)} [{Plain(story)}]",
        EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject { Text: { Length: > 0 } shapeText } } => $"text object {Math.Round(item.Size.Width.Value / 20.0)} [{string.Join("|", shapeText.Split('\n').Select(line => line.Trim()))}]",
        EmbeddedContent embedded => $"object {embedded.Value.GetType().Name} {Math.Round(item.Size.Width.Value / 20.0)}x{Math.Round(item.Size.Height.Value / 20.0)}",
        _ => $"object {item.Content.GetType().Name}",
    };

    private string Plain(BlockContainer container) =>
        string.Join("|", container.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text.Trim()));

    private void Blocks(StringBuilder text, BlockContainer container, string indent)
    {
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    var content = paragraph.Content;
                    var pieces = new List<(string Flags, string Text)>();
                    foreach (var run in content.Runs.Where(run => run.Text.Length > 0))
                    {
                        var format = _resolver.EffectiveRun(_styles, content, run);
                        var flags = $"{(format.Bold ? "B" : "")}{(format.Italic ? "I" : "")}";
                        var piece = collapseWhitespace ? System.Text.RegularExpressions.Regex.Replace(run.Text, @"\s+", " ") : run.Text;
                        if (collapseWhitespace && piece.Trim().Length == 0 && pieces.Count > 0 && !pieces[^1].Flags.Contains('F'))
                        {
                            pieces[^1] = (pieces[^1].Flags, pieces[^1].Text + piece);
                            continue;
                        }
                        if (run.Field is { } field && field != TextFieldKind.None) pieces.Add((flags + "F" + field, string.Empty));
                        else if (pieces.Count > 0 && pieces[^1].Flags == flags) pieces[^1] = (flags, pieces[^1].Text + piece);
                        else pieces.Add((flags, piece));
                    }
                    var runs = string.Concat(pieces.Select(item => item.Flags.Contains('F') ? item.Flags : $"{item.Flags}<{(collapseWhitespace ? System.Text.RegularExpressions.Regex.Replace(item.Text, @"\s+", " ").Trim() : item.Text)}>"));
                    text.AppendLine($"{indent}P {(content.ListStyleName is null ? "" : "list" + content.BulletLevel)} {runs}");
                    break;
                case TableBlock table:
                    text.AppendLine($"{indent}T {table.Columns.Count} cols");
                    foreach (var row in table.Rows)
                    {
                        text.AppendLine($"{indent}  row");
                        foreach (var cell in row.Cells.Where(cell => !cell.Covered))
                        {
                            text.AppendLine($"{indent}    cell {cell.ColumnSpan}x{cell.RowSpan}");
                            Blocks(text, cell.Content, indent + "      ");
                        }
                    }
                    break;
                case SectionBlock section:
                    Blocks(text, section.Content, indent);
                    break;
            }
        }
    }
}
