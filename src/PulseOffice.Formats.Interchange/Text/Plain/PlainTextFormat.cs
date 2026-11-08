using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Interchange.Text.Assembly;
using PulseOffice.Formats.Interchange.Text.Outline;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Interchange.Text.Plain;

public sealed class PlainTextReader(ITextEncodingDetector detector, IDirectFormatting direct, ITextDocumentFactory factory) : ITextDocumentReader
{
    private const long MaximumBytes = 256L * 1024 * 1024;

    public TextDocumentFormat Format => TextInterchangeFormats.PlainText;

    public bool CanRead(Stream stream) => false;

    public OperationResult<TextDocument> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > MaximumBytes)
                    return OperationResult<TextDocument>.Fail(Diagnostic.Error("TXT102", "The text file is too large to open."));
                buffer.Write(chunk, 0, read);
            }

            var text = detector.Decode(buffer.ToArray()).Text;
            var document = factory.Create();
            var assembler = new TextDocumentAssembler(document, direct);
            var start = 0;
            for (var i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && text[i] is not ('\n' or '\r' or '\f' or '\u2028' or '\u2029' or '\u0085')) continue;
                assembler.Write(Clean(text.AsSpan(start, i - start)), RunShape.Plain);
                assembler.EndParagraph(ParagraphShape.Plain);
                if (i < text.Length - 1 && text[i] == '\r' && text[i + 1] == '\n') i++;
                start = i + 1;
            }

            if (document.Body.Count > 1 && document.Body.Blocks[^1] is ParagraphBlock { Length: 0 } && (text.EndsWith('\n') || text.EndsWith('\r')))
                document.Body.Remove(document.Body.Blocks[^1]);
            return OperationResult<TextDocument>.Ok(document);
        }
        catch (IOException exception)
        {
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("TXT101", $"Reading the text file failed: {exception.Message}").With(exception.Message));
        }
    }

    private string Clean(ReadOnlySpan<char> line)
    {
        var text = new StringBuilder(line.Length);
        foreach (var character in line)
            if (character == '\t' || !char.IsControl(character)) text.Append(character);
        return text.ToString();
    }
}

public sealed class PlainTextWriter(IParagraphSemantics semantics, string? lineEnding = null) : ITextDocumentWriter
{
    private readonly string _lineEnding = lineEnding ?? Environment.NewLine;
    private readonly ListNumbering _numbering = new();

    public TextDocumentFormat Format => TextInterchangeFormats.PlainText;

    public FidelityReport CheckFidelity(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var losses = new List<Diagnostic>
        {
            Diagnostic.Warning("TXT001", "Plain text keeps only the words: formatting, styles, tables, page layout and fields become plain text."),
        };
        if (document.Objects.All.Count > 0 || document.Notes.All.Count > 0 || document.Comments.All.Count > 0 || document.Changes.All.Count > 0)
            losses.Add(Diagnostic.Warning("TXT002", "Images, objects, notes, comments and tracked changes are not written to plain text."));
        return new FidelityReport(losses);
    }

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = _lineEnding };
            Blocks(writer, document.Styles, document.Body, new ListThreadCounters(document.Styles));
            writer.Flush();
            return OperationResult<bool>.Ok(true);
        }
        catch (IOException exception)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("TXT100", $"Writing the text file failed: {exception.Message}").With(exception.Message));
        }
    }

    private void Blocks(StreamWriter writer, StyleSheet sheet, BlockContainer container, ListThreadCounters counters)
    {
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    var placement = semantics.List(sheet, paragraph.Content);
                    var value = placement.Kind == ListKind.Numbered ? counters.Next(paragraph.Content with { ListStyleName = placement.StyleName }, placement.Level) : 0;
                    var label = paragraph.Content.ListHeader ? string.Empty : _numbering.Label(placement, value);
                    var text = paragraph.Text.Replace("\n", _lineEnding, StringComparison.Ordinal);
                    writer.WriteLine(label.Length > 0 ? label + " " + text : text);
                    break;
                case TableBlock table:
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells).Where(cell => !cell.Covered))
                        Blocks(writer, sheet, cell.Content, new ListThreadCounters(sheet));
                    break;
                case SectionBlock section:
                    Blocks(writer, sheet, section.Content, counters);
                    break;
            }
        }
    }
}
