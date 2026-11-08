using PulseOffice.Document.Fonts;
using PulseOffice.Document.Forms;
using PulseOffice.Document.Metadata;
using PulseOffice.Document.Versions;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Security;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public sealed class DocumentPropertiesConverter : SnapshotConverter<DocumentProperties>
{
    protected override DocumentProperties Read(SnapshotReader node)
    {
        var properties = new DocumentProperties
        {
            Title = node.Get<string>("title"),
            Subject = node.Get<string>("subject"),
            Description = node.Get<string>("description"),
            Author = node.Get<string>("author"),
            LastModifiedBy = node.Get<string>("lastModifiedBy"),
            Created = node.Get<DateTimeOffset?>("created"),
            Modified = node.Get<DateTimeOffset?>("modified"),
            Generator = node.Get<string>("generator"),
            Language = node.Get<string>("language"),
            EditingCycles = node.Get("editingCycles", 0),
        };
        properties.Keywords.AddRange(node.Get<List<string>>("keywords", []));
        foreach (var (key, value) in node.Get<Dictionary<string, string>>("custom", [])) properties.Custom[key] = value;
        foreach (var (key, value) in node.Get<Dictionary<string, CustomPropertyKind>>("customKinds", [])) properties.CustomKinds[key] = value;
        return properties;
    }

    protected override void Write(DocumentProperties value, SnapshotWriter node) =>
        node.Put("title", value.Title).Put("subject", value.Subject).Put("description", value.Description).Put("author", value.Author)
            .Put("lastModifiedBy", value.LastModifiedBy).Put("created", value.Created).Put("modified", value.Modified).Put("generator", value.Generator)
            .Put("language", value.Language).Put("editingCycles", value.EditingCycles).Put("keywords", value.Keywords)
            .Put("custom", value.Custom).Put("customKinds", value.CustomKinds);
}

public sealed record BibliographyEntry(string Identifier, string Type, Dictionary<string, string> Values);

public sealed class TextDocumentConverter : SnapshotConverter<TextDocument>
{
    protected override TextDocument Read(SnapshotReader node)
    {
        var document = new TextDocument(node.Get("styles", new StyleSheet()))
        {
            InitialPageStyle = node.Get("initialPageStyle", DefaultStyleNames.Standard),
            MacroSignature = node.Get<MacroSignature>("macroSignature"),
            FormSettings = node.Get("formSettings", new FormDocumentSettings()),
            Fonts = node.Get("fonts", DocumentFonts.Empty),
            FileName = node.Get<string>("fileName"),
            Protection = node.Get("protection", PulseOffice.Documents.Protection.DocumentProtection.None),
            Marks = node.Get("markProtection", PulseOffice.Documents.Protection.MarkProtection.None),
            LineNumbering = node.Get<LineNumbering>("lineNumbering"),
            IsMaster = node.Get("master", false),
            MergeSource = node.Get<PulseOffice.Documents.Fields.MailMergeSource>("mergeSource"),
        };
        document.Bibliography.Replace(node.Get<List<BibliographyEntry>>("bibliography", []).Select(entry => new PulseOffice.Documents.Fields.BibliographySource(entry.Identifier, entry.Type, entry.Values)));
        document.Body.Replace(node.Get("body", new BlockContainer()).Blocks);
        document.Objects.Replace(node.Get<List<AnchoredObject>>("objects", []));
        document.Comments.Replace(node.Get<List<Comment>>("comments", []));
        document.Notes.Replace(node.Get<List<Note>>("notes", []));
        document.Notes.FootnoteSettings = node.Get("footnoteSettings", document.Notes.FootnoteSettings);
        document.Notes.EndnoteSettings = node.Get("endnoteSettings", document.Notes.EndnoteSettings);
        document.Changes.Replace(node.Get<List<TrackedChange>>("changes", []));
        document.Changes.IsRecording = node.Get("recording", false);
        document.Changes.ProtectionKey = node.Get<byte[]>("protectionKey");
        document.Changes.ShowChanges = node.Get("showChanges", true);
        foreach (var (name, value) in node.Get<Dictionary<string, string>>("variables", [])) document.Variables[name] = value;
        if (node.Get<DocumentProperties>("properties") is { } properties) document.Properties.CopyFrom(properties);
        foreach (var macro in node.Get<List<DocumentMacro>>("macros", [])) document.Macros.Store(macro);
        foreach (var version in node.Get<List<DocumentVersion>>("versions", [])) document.Versions.Adopt(version);
        foreach (var id in node.Get<List<string>>("ids", [])) document.Ids.Reserve(id);
        return document;
    }

    protected override void Write(TextDocument value, SnapshotWriter node) =>
        node.Put("styles", value.Styles).Put("initialPageStyle", value.InitialPageStyle).Put("macroSignature", value.MacroSignature)
            .Put("formSettings", value.FormSettings).Put("fonts", value.Fonts).Put("fileName", value.FileName).Put("protection", value.Protection).Put("body", value.Body)
            .Put("objects", value.Objects.All).Put("comments", value.Comments.All).Put("notes", value.Notes.All)
            .Put("footnoteSettings", value.Notes.FootnoteSettings).Put("endnoteSettings", value.Notes.EndnoteSettings)
            .Put("changes", value.Changes.All).Put("recording", value.Changes.IsRecording).Put("protectionKey", value.Changes.ProtectionKey)
            .Put("showChanges", value.Changes.ShowChanges).Put("variables", value.Variables).Put("properties", value.Properties)
            .Put("macros", value.Macros.All).Put("versions", value.Versions.All).Put("ids", Ids(value))
            .Put("markProtection", value.Marks).Put("lineNumbering", value.LineNumbering).Put("master", value.IsMaster).Put("mergeSource", value.MergeSource)
            .Put("bibliography", value.Bibliography.All.Select(source => new BibliographyEntry(source.Identifier, source.Type, new Dictionary<string, string>(source.Values, StringComparer.Ordinal))).ToList());

    private IReadOnlyList<string> Ids(TextDocument document)
    {
        var tree = new BlockTree();
        var blocks = new[] { document.Body }
            .Concat(document.Notes.All.Select(note => note.Content))
            .Concat(document.Comments.All.Select(comment => comment.Content))
            .Concat(document.Objects.All.Select(item => item.Content.Story).OfType<BlockContainer>())
            .Concat(document.Styles.Page.All.SelectMany(page => new[] { page.Header, page.Footer }).SelectMany(part => new[] { part.Default, part.First, part.Even }).OfType<BlockContainer>())
            .SelectMany(tree.Blocks)
            .Select(block => block.Id);
        return [.. blocks.Concat(document.Notes.All.Select(note => note.Id)).Concat(document.Comments.All.Select(comment => comment.Id))
            .Concat(document.Objects.All.Select(item => item.Id)).Concat(document.Changes.All.Select(change => change.Id)).Distinct(StringComparer.Ordinal)];
    }
}
