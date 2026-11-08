using System.Text.RegularExpressions;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Tests.Odt;

namespace PulseOffice.Formats.Interchange.Tests.Text;

internal sealed class ModelSnapshot
{
    private readonly TextDocumentDescriber _describer = new();

    public string Content(TextDocument document)
    {
        var description = _describer.Describe(document);
        description = description[description.IndexOf("body:", StringComparison.Ordinal)..];
        var ids = Identifiers(document).Distinct().Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => "#" + pair.index);
        if (ids.Count > 0)
        {
            var pattern = new Regex(@"(?<![\w#])(" + string.Join("|", ids.Keys.OrderByDescending(id => id.Length).Select(Regex.Escape)) + @")(?!\w)");
            description = pattern.Replace(description, match => ids[match.Value]);
        }
        description = Regex.Replace(description, @"(?m)^(\s+(image|packaged)) \S+", "$1 #content");
        return description.Replace(" style= outline=", " style=" + DefaultStyleNames.Standard + " outline=", StringComparison.Ordinal);
    }

    private IEnumerable<string> Identifiers(TextDocument document)
    {
        foreach (var id in Containers(document.Body)) yield return id;
        foreach (var note in document.Notes.All)
        {
            yield return note.Id;
            foreach (var id in Containers(note.Content)) yield return id;
        }
        foreach (var comment in document.Comments.All)
        {
            yield return comment.Id;
            foreach (var id in Containers(comment.Content)) yield return id;
        }
        foreach (var item in document.Objects.All.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            yield return item.Id;
            if (item.Content is TextBoxContent box)
                foreach (var id in Containers(box.Content)) yield return id;
        }
    }

    private IEnumerable<string> Containers(BlockContainer container)
    {
        foreach (var block in container.Blocks)
        {
            yield return block.Id;
            var inner = block switch
            {
                TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Containers(cell.Content)),
                SectionBlock section => Containers(section.Content),
                _ => [],
            };
            foreach (var id in inner) yield return id;
        }
    }
}
