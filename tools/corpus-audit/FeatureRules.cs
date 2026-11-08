using System.Xml.Linq;

namespace PulseOffice.Tools.CorpusAudit;

public enum Comparison { Count, Presence }

public sealed record FeatureRule(string Feature, string Element, Func<XElement, RuleContext, int> Weight);

public sealed record FeatureDefinition(string Feature, Comparison Comparison);

public sealed class NamespaceKeys
{
    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal)
    {
        ["urn:oasis:names:tc:opendocument:xmlns:office:1.0"] = "office",
        ["urn:oasis:names:tc:opendocument:xmlns:table:1.0"] = "table",
        ["urn:oasis:names:tc:opendocument:xmlns:text:1.0"] = "text",
        ["urn:oasis:names:tc:opendocument:xmlns:form:1.0"] = "form",
        ["urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"] = "draw",
        ["urn:oasis:names:tc:opendocument:xmlns:chart:1.0"] = "chart",
        ["urn:oasis:names:tc:opendocument:xmlns:presentation:1.0"] = "presentation",
        ["urn:oasis:names:tc:opendocument:xmlns:animation:1.0"] = "anim",
        ["urn:oasis:names:tc:opendocument:xmlns:style:1.0"] = "style",
        ["urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"] = "smil",
        ["urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0"] = "calcext",
        ["urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0"] = "loext",
        ["http://openoffice.org/2009/office"] = "officeooo",
        ["http://openoffice.org/2000/library"] = "library",
        ["urn:oasis:names:tc:opendocument:xmlns:script:1.0"] = "script",
        ["http://schemas.openxmlformats.org/spreadsheetml/2006/main"] = "x",
        ["http://purl.oclc.org/ooxml/spreadsheetml/main"] = "x",
        ["http://schemas.openxmlformats.org/wordprocessingml/2006/main"] = "w",
        ["http://purl.oclc.org/ooxml/wordprocessingml/main"] = "w",
        ["http://schemas.openxmlformats.org/presentationml/2006/main"] = "p",
        ["http://purl.oclc.org/ooxml/presentationml/main"] = "p",
        ["http://schemas.openxmlformats.org/drawingml/2006/main"] = "a",
        ["http://purl.oclc.org/ooxml/drawingml/main"] = "a",
        ["http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"] = "xdr",
        ["http://purl.oclc.org/ooxml/drawingml/spreadsheetDrawing"] = "xdr",
        ["http://schemas.openxmlformats.org/drawingml/2006/chart"] = "c",
        ["http://purl.oclc.org/ooxml/drawingml/chart"] = "c",
        ["http://schemas.openxmlformats.org/drawingml/2006/picture"] = "pic",
        ["http://purl.oclc.org/ooxml/drawingml/picture"] = "pic",
        ["http://schemas.microsoft.com/office/word/2010/wordprocessingShape"] = "wps",
        ["http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"] = "wpg",
        ["urn:schemas-microsoft-com:vml"] = "v",
        ["http://schemas.microsoft.com/office/powerpoint/2010/main"] = "p14",
        ["http://schemas.microsoft.com/office/powerpoint/2018/8/main"] = "p188",
        ["http://schemas.openxmlformats.org/markup-compatibility/2006"] = "mc",
        ["urn:pulseoffice:text"] = "pt",
    };

    public string Key(XName name) =>
        _keys.TryGetValue(name.NamespaceName, out var key) ? key + ":" + name.LocalName : name.LocalName;

    public string? Attribute(XElement element, string key) =>
        element.Attributes().FirstOrDefault(attribute => Key(attribute.Name) == key || attribute.Name.LocalName == key)?.Value;

    public bool HasChild(XElement element, params string[] keys) =>
        element.Elements().Any(child => keys.Contains(Key(child.Name)));

    public bool HasAncestor(XElement element, string key) =>
        element.Ancestors().Any(ancestor => Key(ancestor.Name) == key);

    public bool HasDescendant(XElement element, params string[] keys) =>
        element.Descendants().Any(child => keys.Contains(Key(child.Name)));

    public bool HasText(XElement element, string textElement) =>
        element.Descendants().Any(child => Key(child.Name) == textElement && !string.IsNullOrWhiteSpace(child.Value));
}

public interface IFeatureCatalog
{
    IReadOnlyList<FeatureDefinition> Definitions { get; }

    IReadOnlyList<FeatureRule> RulesFor(string extension);

    bool PartIncluded(string extension, string part);

    bool FeatureIncluded(string extension, string part, string feature);
}

public sealed class FeatureCatalog : IFeatureCatalog
{
    private static readonly string[] OdfShapes =
        ["custom-shape", "rect", "ellipse", "line", "polyline", "polygon", "path", "connector", "measure", "circle", "regular-polygon"];

    private static readonly string[] OdfFields =
        ["page-number", "page-count", "date", "time", "author-name", "author-initials", "title", "subject", "file-name", "sequence",
         "reference-ref", "bookmark-ref", "sequence-ref", "note-ref", "user-field-get", "variable-get", "variable-set", "expression",
         "chapter", "word-count", "character-count", "paragraph-count", "initial-creator", "creation-date", "modification-date",
         "conditional-text", "hidden-text", "hidden-paragraph", "database-display", "placeholder", "drop-down", "text-input",
         "user-defined", "keywords", "description", "page-variable-get", "sheet-name", "sender-firstname", "sender-lastname",
         "print-date", "print-time", "printed-by", "editing-cycles", "editing-duration", "template-name", "creation-time",
         "modification-time", "execute-macro", "user-field-input", "variable-input", "page-variable-set", "database-name",
         "database-row-number", "database-next", "database-row-select", "dde-connection", "measure", "script"];

    private static readonly string[] OdfIndexes =
        ["table-of-content", "alphabetical-index", "illustration-index", "table-index", "object-index", "user-index", "bibliography"];

    private static readonly string[] WordRevisions =
        ["w:ins", "w:del", "w:moveFrom", "w:moveTo", "w:rPrChange", "w:pPrChange", "w:tblPrChange", "w:sectPrChange"];

    private static readonly string[] WordIndexFields = ["TOC", "INDEX", "BIBLIOGRAPHY"];

    private static readonly string[] WordNonFields =
        ["HYPERLINK", "TOC", "INDEX", "BIBLIOGRAPHY", "PAGEREF", "XE", "TC", "FORMTEXT", "FORMCHECKBOX", "FORMDROPDOWN", "CITATION", "SHAPE", "EMBED", "ADDIN", "LINK"];

    private readonly Dictionary<string, IReadOnlyList<FeatureRule>> _rules;

    public FeatureCatalog()
    {

        _rules = new Dictionary<string, IReadOnlyList<FeatureRule>>(StringComparer.OrdinalIgnoreCase)
        {
            [".ods"] = [.. OdfCommon(false), .. OdsOnly()],
            [".odt"] = [.. OdfCommon(true), .. OdtOnly()],
            [".odp"] = [.. OdfCommon(false), .. OdpOnly()],
            [".xlsx"] = Xlsx(),
            [".docx"] = Docx(),
            [".pptx"] = Pptx(),
        };
    }

    public IReadOnlyList<FeatureDefinition> Definitions { get; } =
    [
        new("filters", Comparison.Count),
        new("validations", Comparison.Presence),
        new("conditional-formats", Comparison.Presence),
        new("pivots", Comparison.Count),
        new("charts", Comparison.Count),
        new("formulas", Comparison.Count),
        new("merges", Comparison.Count),
        new("names", Comparison.Count),
        new("revisions", Comparison.Presence),
        new("fields", Comparison.Presence),
        new("indexes", Comparison.Presence),
        new("frames", Comparison.Presence),
        new("shapes", Comparison.Presence),
        new("groups", Comparison.Count),
        new("links", Comparison.Presence),
        new("images", Comparison.Count),
        new("lists", Comparison.Presence),
        new("notes", Comparison.Count),
        new("comments", Comparison.Count),
        new("tables", Comparison.Count),
        new("sections", Comparison.Count),
        new("animations", Comparison.Count),
        new("transitions", Comparison.Presence),
        new("media", Comparison.Count),
        new("macros", Comparison.Count),
    ];

    public IReadOnlyList<FeatureRule> RulesFor(string extension) => _rules[extension];

    public bool PartIncluded(string extension, string part) => extension.ToLowerInvariant() switch
    {
        ".ods" or ".odp" => part.EndsWith("content.xml", StringComparison.Ordinal) || MacroPart(part),
        ".odt" => part.EndsWith("content.xml", StringComparison.Ordinal) || part == "styles.xml" || MacroPart(part),
        ".xlsx" => part.StartsWith("xl/", StringComparison.Ordinal) && part.EndsWith(".xml", StringComparison.Ordinal),
        ".docx" => part.StartsWith("word/", StringComparison.Ordinal) && !part.StartsWith("word/glossary/", StringComparison.Ordinal)
            && part != "word/settings.xml" && part.EndsWith(".xml", StringComparison.Ordinal),
        ".pptx" => (part.StartsWith("ppt/slides/", StringComparison.Ordinal) || part.StartsWith("ppt/charts/", StringComparison.Ordinal)
            || part.StartsWith("ppt/comments/", StringComparison.Ordinal)) && part.EndsWith(".xml", StringComparison.Ordinal),
        _ => false,
    };

    public bool FeatureIncluded(string extension, string part, string feature) =>
        !part.Contains('/', StringComparison.Ordinal) || !extension.StartsWith(".od", StringComparison.OrdinalIgnoreCase) || feature == "charts"
        || (feature == "macros" && MacroPart(part));

    private static bool MacroPart(string part) =>
        (part.StartsWith("Basic/", StringComparison.Ordinal) && part.EndsWith("/script-lb.xml", StringComparison.Ordinal))
        || part == "Scripts/pulseoffice/index.xml";

    private static int One(XElement element, RuleContext keys) => 1;

    private static FeatureRule Rule(string feature, string element, Func<XElement, RuleContext, bool>? when = null) =>
        new(feature, element, when is null ? One : (node, keys) => when(node, keys) ? 1 : 0);

    private static List<FeatureRule> OdfCommon(bool textShapesAreFrames)
    {
        var rules = new List<FeatureRule>
        {
            Rule("charts", "chart:chart"),
            Rule("macros", "library:element"),
            Rule("macros", "script:module"),
            Rule("groups", "draw:g", (node, keys) => !keys.HasAncestor(node, "presentation:notes")),
            Rule("links", "text:a"),
            Rule("links", "draw:a"),
            Rule("links", "presentation:event-listener", (node, keys) => keys.Attribute(node, "action") is not (null or "none")),
            Rule("comments", "office:annotation", (node, keys) => !keys.HasAncestor(node, "presentation:notes")),
            Rule("comments", "officeooo:annotation"),
            Rule("images", "draw:frame", (node, keys) => keys.HasChild(node, "draw:image")
                && !keys.HasChild(node, "draw:object", "draw:object-ole", "draw:plugin") && !keys.HasAncestor(node, "presentation:notes")),
        };
        rules.AddRange(OdfShapes.Select(shape => Rule("shapes", "draw:" + shape, (node, keys) => !keys.HasAncestor(node, "presentation:notes")
            && !(textShapesAreFrames && OdfTextShape(node, keys)) && !OdfInvisible(node, keys))));
        return rules;
    }

    private static List<FeatureRule> OdsOnly() =>
    [
        Rule("filters", "table:database-range", (node, keys) => keys.HasChild(node, "table:filter")
            || keys.Attribute(node, "table:display-filter-buttons") == "true"),
        Rule("validations", "table:content-validation"),
        Rule("conditional-formats", "calcext:conditional-format"),
        Rule("pivots", "table:data-pilot-table"),
        new("formulas", "table:table-cell", (node, keys) => keys.Attribute(node, "table:formula") is not { } formula || LogicalConstant(formula) ? 0
            : int.TryParse(keys.Attribute(node, "table:number-columns-repeated"), out var repeated) ? repeated : 1),
        Rule("merges", "table:table-cell", (node, keys) => Spanned(keys.Attribute(node, "table:number-columns-spanned"))
            || Spanned(keys.Attribute(node, "table:number-rows-spanned"))),
        Rule("names", "table:named-range", (node, keys) => !(keys.Attribute(node, "table:name") ?? "").StartsWith("_xlnm.", StringComparison.Ordinal)),
        Rule("names", "table:named-expression", (node, keys) => !(keys.Attribute(node, "table:name") ?? "").StartsWith("_xlnm.", StringComparison.Ordinal)),
        Rule("shapes", "draw:control"),
    ];

    private static List<FeatureRule> OdtOnly()
    {
        var rules = new List<FeatureRule>
        {
            Rule("revisions", "text:changed-region", AnchoredChange),
            Rule("frames", "draw:frame", (node, keys) => keys.HasChild(node, "draw:text-box")),
            Rule("lists", "text:list", (node, keys) => !keys.HasAncestor(node, "text:list") && !keys.HasAncestor(node, "office:annotation")
                && keys.Attribute(node, "text:style-name") != "Outline" && !(keys.Attribute(node, "text:style-name") is { } named && keys.SilentListStyles.Contains(named))
                && node.Descendants().Any(item => keys.Key(item.Name) == "text:list-item" && keys.HasChild(item, "text:p", "text:h") && !keys.HasAncestor(item, "office:annotation"))),
            Rule("lists", "text:p", StyleListed),
            Rule("lists", "text:h", StyleListed),
            Rule("notes", "text:note"),
            Rule("tables", "table:table"),
            Rule("sections", "text:section"),
        };
        rules.AddRange(OdfShapes.Select(shape => Rule("frames", "draw:" + shape, OdfTextShape)));
        rules.AddRange(OdfFields.Select(field => Rule("fields", "text:" + field, (node, keys) => keys.Attribute(node, "text:reference-format") != "page"
            && (keys.HasAncestor(node, "office:body") || keys.HasAncestor(node, "style:master-page")))));
        rules.Add(Rule("fields", "text:span", (node, keys) => keys.Attribute(node, "pt:field-instruction") is not null
            && (keys.HasAncestor(node, "office:body") || keys.HasAncestor(node, "style:master-page"))));
        rules.Add(Rule("fields", "pt:field-code", (node, keys) => keys.Attribute(node, "pt:part") == "begin"
            && (keys.HasAncestor(node, "office:body") || keys.HasAncestor(node, "style:master-page"))));
        rules.AddRange(OdfIndexes.Select(index => Rule("indexes", "text:" + index)));
        return rules;
    }

    private static List<FeatureRule> OdpOnly() =>
    [
        Rule("tables", "table:table"),
        Rule("shapes", "draw:frame", (node, keys) => keys.HasChild(node, "draw:text-box") && keys.Attribute(node, "presentation:class") is null
            && !keys.HasAncestor(node, "presentation:notes")),
        Rule("animations", "anim:par", OdfEffect),
        Rule("animations", "anim:iterate", OdfEffect),
        Rule("transitions", "style:drawing-page-properties", (node, keys) => keys.Attribute(node, "smil:type") is not null
            || keys.Attribute(node, "presentation:transition-type") == "automatic"),
        Rule("media", "draw:plugin"),
    ];

    private static List<FeatureRule> Xlsx() =>
    [
        Rule("filters", "x:autoFilter"),
        Rule("filters", "x:definedName", (node, keys) => keys.Attribute(node, "name") == "_xlnm.Criteria" && !keys.HasAncestor(node, "x:externalBook")),
        Rule("validations", "x:dataValidation"),
        Rule("conditional-formats", "x:conditionalFormatting"),
        Rule("pivots", "x:pivotTableDefinition"),
        Rule("charts", "c:chartSpace"),
        Rule("formulas", "x:f", (node, keys) => !LogicalConstant(node.Value) && (node.Value.Trim().Length > 0 || keys.Attribute(node, "t") == "shared") && !keys.HasAncestor(node, "x:revisions")),
        Rule("merges", "x:mergeCell"),
        Rule("names", "x:definedName", (node, keys) => !(keys.Attribute(node, "name") ?? "").StartsWith("_xlnm.", StringComparison.Ordinal)
            && !keys.HasAncestor(node, "x:externalBook")),
        Rule("comments", "x:comment"),
        Rule("links", "x:hyperlink", (node, keys) => keys.Attribute(node, "id") is { Length: > 0 } || keys.Attribute(node, "location") is { Length: > 0 }),
        Rule("links", "a:hlinkClick", (node, keys) => !string.IsNullOrEmpty(keys.Attribute(node, "r:id") ?? keys.Attribute(node, "id"))),
        Rule("images", "xdr:pic"),
        Rule("shapes", "xdr:sp", (node, keys) => !Invisible(node, keys, "xdr:spPr")),
        Rule("shapes", "xdr:cxnSp"),
        Rule("shapes", "x:control"),
        Rule("groups", "xdr:grpSp"),
    ];

    private static List<FeatureRule> Docx()
    {
        var rules = new List<FeatureRule>
        {
            Rule("charts", "c:chart", (node, keys) => keys.Attribute(node, "id") is { Length: > 0 }),
            Rule("fields", "w:fldSimple", (node, keys) => !IsNonField(keys.Attribute(node, "w:instr") ?? "")),
            Rule("fields", "w:fldChar", (node, keys) => keys.Attribute(node, "w:fldCharType") == "begin" && !IsNonField(Instruction(node, keys))),
            Rule("indexes", "w:instrText", (node, keys) => IsIndexField(node.Value)),
            Rule("indexes", "w:fldSimple", (node, keys) => IsIndexField(keys.Attribute(node, "w:instr") ?? "")),
            Rule("frames", "w:framePr", (node, keys) => node.Ancestors().All(ancestor => keys.Key(ancestor.Name) != "w:styles") && keys.Attribute(node, "dropCap") is null or "none" && node.Attributes().Any(attribute => attribute.Name.LocalName is not ("wrap" or "fillcolor"))),
            Rule("frames", "wps:wsp", WordTextBox),
            Rule("frames", "v:textbox", (node, keys) => keys.HasText(node, "w:t")),
            Rule("shapes", "wps:wsp", (node, keys) => !WordTextBox(node, keys)),
            Rule("shapes", "v:rect", VmlShape),
            Rule("shapes", "v:oval", VmlShape),
            Rule("shapes", "v:line", VmlShape),
            Rule("shapes", "v:roundrect", VmlShape),
            Rule("shapes", "v:shape", VmlShape),
            Rule("groups", "wpg:wgp"),
            Rule("groups", "wpg:grpSp"),
            Rule("groups", "v:group"),
            Rule("links", "w:hyperlink"),
            Rule("links", "a:hlinkClick", (node, keys) => !string.IsNullOrEmpty(keys.Attribute(node, "r:id") ?? keys.Attribute(node, "id"))),
            Rule("images", "pic:pic"),
            Rule("images", "v:imagedata", (node, keys) => !keys.HasAncestor(node, "w:object") && !keys.HasAncestor(node, "w:numPicBullet")),
            Rule("lists", "w:p", (node, keys) => (keys.HasAncestor(node, "w:body") || keys.HasAncestor(node, "w:hdr") || keys.HasAncestor(node, "w:ftr")) && WordListParagraph(node, keys)),
            Rule("notes", "w:footnoteReference"),
            Rule("notes", "w:endnoteReference"),
            Rule("comments", "w:comment"),
            Rule("tables", "w:tbl"),
        };
        rules.AddRange(WordRevisions.Select(revision => Rule("revisions", revision)));
        return rules;
    }

    private static List<FeatureRule> Pptx() =>
    [
        Rule("charts", "c:chartSpace"),
        Rule("groups", "p:grpSp", (node, keys) => node.Elements().Count(child => keys.Key(child.Name) is "p:sp" or "p:pic" or "p:grpSp" or "p:cxnSp" or "p:graphicFrame") > 1),
        Rule("links", "a:hlinkClick", (node, keys) => !string.IsNullOrEmpty(keys.Attribute(node, "r:id") ?? keys.Attribute(node, "id"))
            || keys.Attribute(node, "action") is not (null or "ppaction://media")),
        Rule("comments", "p:cm"),
        Rule("comments", "p188:cm"),
        Rule("images", "p:pic", (node, keys) => !keys.HasDescendant(node, "a:videoFile", "a:audioFile", "a:wavAudioFile", "p14:media")
            && !keys.HasAncestor(node, "p:oleObj")),
        Rule("shapes", "p:sp", (node, keys) => !keys.HasDescendant(node, "p:ph") && !Invisible(node, keys, "p:spPr")),
        Rule("shapes", "p:cxnSp"),
        Rule("tables", "a:tbl"),
        Rule("animations", "p:cTn", (node, keys) => keys.Attribute(node, "presetClass") is not null && keys.HasDescendant(node, "p:spTgt")),
        Rule("transitions", "p:transition", (node, keys) => node.HasElements || keys.Attribute(node, "advTm") is not null),
        Rule("media", "p:pic", (node, keys) => keys.HasDescendant(node, "a:videoFile", "a:audioFile", "a:wavAudioFile", "p14:media")),
    ];

    private static string Instruction(XElement fieldChar, RuleContext keys) =>
        fieldChar.Parent?.ElementsAfterSelf().SelectMany(run => run.Elements()).FirstOrDefault(child => keys.Key(child.Name) == "w:instrText")?.Value ?? "";

    private static bool IsNonField(string instruction)
    {
        var trimmed = instruction.TrimStart();
        return trimmed.Length == 0 || WordNonFields.Any(name => trimmed.StartsWith(name, StringComparison.OrdinalIgnoreCase)
            && (trimmed.Length == name.Length || !char.IsLetter(trimmed[name.Length])));
    }

    private static bool WordListParagraph(XElement paragraph, RuleContext keys)
    {
        var properties = keys.Child(paragraph, "w:pPr");
        if (keys.Child(keys.Child(properties, "w:numPr"), "w:numId") is { } numId) return keys.Attribute(numId, "w:val") is { } value && value != "0"
            && !keys.SilentNumberings.Contains(RuleContextFactory.SilentKey(value, keys.Child(keys.Child(properties, "w:numPr"), "w:ilvl") is { } ilvl ? keys.Attribute(ilvl, "w:val") ?? "0" : "0"));
        return keys.Child(properties, "w:pStyle") is { } style && keys.Attribute(style, "w:val") is { } name && keys.NumberedStyles.Contains(name);
    }

    private static bool Invisible(XElement shape, RuleContext keys, string propertiesKey)
    {
        var properties = shape.Elements().FirstOrDefault(child => keys.Key(child.Name) == propertiesKey);
        var line = properties?.Elements().FirstOrDefault(child => keys.Key(child.Name) == "a:ln");
        var noLine = line is null ? !keys.HasChild(shape, "xdr:style", "p:style") : keys.HasChild(line, "a:noFill");
        return properties is not null && keys.HasChild(properties, "a:noFill") && noLine && !keys.HasText(shape, "a:t");
    }

    private static bool StyleListed(XElement paragraph, RuleContext keys) =>
        keys.HasAncestor(paragraph, "office:body") && !keys.HasAncestor(paragraph, "text:list") && !keys.HasAncestor(paragraph, "office:annotation")
        && (keys.Attribute(paragraph, "text:style-name") is { } style && keys.ListedParagraphStyles.Contains(style)
            || keys.Key(paragraph.Name) == "text:h" && keys.Attribute(paragraph, "text:outline-level") is { } level && keys.NumberedOutlineLevels.Contains(level));

    private static bool OdfInvisible(XElement shape, RuleContext keys) =>
        keys.Attribute(shape, "draw:style-name") is { } style && keys.InvisibleGraphicStyles.Contains(style)
        && !keys.HasText(shape, "text:p") && !keys.HasText(shape, "text:h");

    private static bool OdfEffect(XElement effect, RuleContext keys) =>
        keys.Attribute(effect, "presentation:preset-class") is not null
        && effect.DescendantsAndSelf().Any(child => keys.Attribute(child, "smil:targetElement") is not null);

    private static bool LogicalConstant(string formula)
    {
        var body = formula.Contains(':', StringComparison.Ordinal) ? formula[(formula.IndexOf(':', StringComparison.Ordinal) + 1)..] : formula;
        return body.TrimStart('=').Trim() is "TRUE()" or "FALSE()";
    }

    private static bool Fontwork(XElement shape, RuleContext keys) =>
        shape.Elements().Any(child => keys.Key(child.Name) == "draw:enhanced-geometry" && keys.Attribute(child, "draw:text-path") == "true");

    private static bool OdfTextShape(XElement shape, RuleContext keys) =>
        !Fontwork(shape, keys) && keys.Key(shape.Name) != "draw:measure" && (keys.HasText(shape, "text:p") || keys.HasText(shape, "text:h"));

    private static bool AnchoredChange(XElement region, RuleContext keys)
    {
        var id = keys.Attribute(region, "text:id") ?? keys.Attribute(region, "xml:id");
        var marks = region.Document?.Descendants()
            .Where(mark => keys.Key(mark.Name) is "text:change" or "text:change-start" or "text:change-end" && keys.Attribute(mark, "text:change-id") == id)
            .Select(mark => keys.Key(mark.Name)).ToHashSet(StringComparer.Ordinal) ?? [];
        return marks.Contains("text:change") || (marks.Contains("text:change-start") && marks.Contains("text:change-end"));
    }

    private static bool WordTextBox(XElement shape, RuleContext keys)
    {
        if (shape.Descendants().Any(child => child.Name.NamespaceName == "urn:pulseoffice:dimension-line")) return false;
        if (shape.Descendants().Any(child => keys.Key(child.Name) == "a:prstTxWarp" && keys.Attribute(child, "prst") is not (null or "textNoShape")
            && child.Ancestors().First(ancestor => keys.Key(ancestor.Name) == "wps:wsp") == shape)) return false;
        if (keys.HasText(shape, "w:t")) return true;
        if (!keys.HasAncestor(shape, "wpg:wgp") && shape.Elements().Any(child => keys.Key(child.Name) == "wps:cNvSpPr" && keys.Attribute(child, "txBox") == "1")) return true;
        if (!keys.HasAncestor(shape, "wpg:wgp") && keys.HasChild(shape, "wps:linkedTxbx")) return true;
        if (!keys.HasChild(shape, "wps:txbx") || keys.HasAncestor(shape, "wpg:wgp")) return false;
        var properties = shape.Elements().FirstOrDefault(child => keys.Key(child.Name) == "wps:spPr");
        var geometry = properties?.Elements().FirstOrDefault(child => keys.Key(child.Name) is "a:prstGeom" or "a:custGeom");
        return (geometry is null || keys.Attribute(geometry, "prst") == "rect") && properties is not null && keys.HasChild(properties, "a:noFill");
    }

    private static bool VmlShape(XElement shape, RuleContext keys) =>
        !keys.HasDescendant(shape, "v:imagedata") && !keys.HasAncestor(shape, "w:numPicBullet") && !keys.HasAncestor(shape, "w:object") && !VmlTextBox(shape, keys)
        && !(keys.Attribute(shape, "filled") is "f" or "false" && keys.Attribute(shape, "stroked") is "f" or "false");

    private static bool VmlTextBox(XElement shape, RuleContext keys) =>
        shape.Descendants().Any(child => keys.Key(child.Name) == "v:textbox" && keys.HasText(child, "w:t"));

    private static bool Spanned(string? value) => int.TryParse(value, out var span) && span > 1;

    private static bool IsIndexField(string instruction)
    {
        var trimmed = instruction.TrimStart();
        return WordIndexFields.Any(name => trimmed.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
