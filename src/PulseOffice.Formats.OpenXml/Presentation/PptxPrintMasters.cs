using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxPrintMasters(IHandoutLayout layout)
{
    public const long PageWidth = 7560000;
    public const long PageHeight = 10692000;

    private const int MinimumFontHundredths = 100;
    private const int MaximumFontHundredths = 400000;

    private const string ExtensionUri = "{8E2F7A51-3C9B-4E0E-9B7A-6B1C0F1D4A21}";
    private static readonly XNamespace Pulse = "urn:pulseoffice:presentation-print";

    private static readonly PageBox HeaderBox = new(0.057, 0.02, 0.43, 0.035);
    private static readonly PageBox FooterBox = new(0.057, 0.945, 0.43, 0.035);
    private static readonly PageBox NumberBox = new(0.513, 0.945, 0.43, 0.035);

    public PptxPrintMasters() : this(new HandoutLayout()) { }

    public void Write(PptxWriteSession session, PptxThemeWriter themes)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(themes);

        var deck = session.Deck;
        var presentation = session.Part.Presentation!;

        var notesPart = session.Part.AddNewPart<NotesMasterPart>();
        themes.Write(notesPart, deck.Theme);
        notesPart.NotesMaster = NotesMaster(deck.NotesMaster);

        var handoutPart = session.Part.AddNewPart<HandoutMasterPart>();
        themes.Write(handoutPart, deck.Theme);
        handoutPart.HandoutMaster = HandoutMaster(deck.Handout);

        var masters = presentation.GetFirstChild<P.SlideMasterIdList>();
        var notesList = new P.NotesMasterIdList(new P.NotesMasterId { Id = session.Part.GetIdOfPart(notesPart) });
        var handoutList = new P.HandoutMasterIdList(new P.HandoutMasterId { Id = session.Part.GetIdOfPart(handoutPart) });
        if (masters is null)
        {
            presentation.PrependChild(handoutList);
            presentation.PrependChild(notesList);
        }
        else
        {
            masters.InsertAfterSelf(notesList);
            notesList.InsertAfterSelf(handoutList);
        }

        if (presentation.GetFirstChild<P.NotesSize>() is { } size)
        {
            size.Cx = PageWidth;
            size.Cy = PageHeight;
        }

        foreach (var slide in deck.Slides)
        {
            if (string.IsNullOrEmpty(slide.Notes)) continue;
            if (!session.RelationshipBySlide.TryGetValue(slide.Id, out var relationship)) continue;
            if (session.Part.GetPartById(relationship) is not SlidePart slidePart) continue;

            var notesSlide = slidePart.AddNewPart<NotesSlidePart>();
            notesSlide.AddPart(notesPart);
            notesSlide.AddPart(slidePart);
            notesSlide.NotesSlide = NotesSlide(deck.NotesMaster, slide.Notes);
        }
    }

    public void Read(PresentationPart part, PulseOffice.Presentations.Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(deck);

        if (part.NotesMasterPart?.NotesMaster is { } notes)
        {
            var tree = notes.CommonSlideData?.ShapeTree;
            var master = deck.NotesMaster;
            var size = notes.NotesStyle?.GetFirstChild<D.Level1ParagraphProperties>()?.GetFirstChild<D.DefaultRunProperties>()?.FontSize?.Value;
            deck.NotesMaster = master with
            {
                SlideBox = BoxOf(Placeholder(tree, P.PlaceholderValues.SlideImage)) ?? master.SlideBox,
                NotesBox = BoxOf(Placeholder(tree, P.PlaceholderValues.Body)) ?? master.NotesBox,
                FontSize = size is >= MinimumFontHundredths and <= MaximumFontHundredths and var hundredths ? hundredths / 100.0 : master.FontSize,
                Header = TextOf(Placeholder(tree, P.PlaceholderValues.Header)),
                Footer = TextOf(Placeholder(tree, P.PlaceholderValues.Footer)),
                PageNumber = Placeholder(tree, P.PlaceholderValues.SlideNumber) is not null,
            };
        }

        if (part.HandoutMasterPart?.HandoutMaster is { } handout)
        {
            var tree = handout.CommonSlideData?.ShapeTree;
            var perPage = SlidesPerPage(handout);
            deck.Handout = deck.Handout with
            {
                SlidesPerPage = perPage is { } count ? layout.Nearest(count) : deck.Handout.SlidesPerPage,
                Header = TextOf(Placeholder(tree, P.PlaceholderValues.Header)),
                Footer = TextOf(Placeholder(tree, P.PlaceholderValues.Footer)),
                PageNumber = Placeholder(tree, P.PlaceholderValues.SlideNumber) is not null,
            };
        }
    }

    public string? NotesOf(SlidePart slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var tree = slide.NotesSlidePart?.NotesSlide?.CommonSlideData?.ShapeTree;
        var body = Placeholder(tree, P.PlaceholderValues.Body)
            ?? tree?.Elements<P.Shape>().FirstOrDefault(shape => shape.TextBody is not null
                && shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape is null
                && shape.TextBody.Descendants<D.Text>().Any(run => run.Text.Trim().Length > 0));
        if (body?.TextBody is not { } text) return null;
        var lines = text.Elements<D.Paragraph>().Select(paragraph => string.Concat(paragraph.Descendants<D.Text>().Select(run => run.Text)));
        var joined = string.Join("\n", lines);
        return joined.Trim().Length == 0 ? null : joined;
    }

    private P.NotesMaster NotesMaster(NotesMaster master)
    {
        uint id = 2;
        var tree = Tree();
        Chrome(tree, ref id, master.Header, master.Footer, master.PageNumber);
        tree.Append(Shape(id++, "Slide Image", P.PlaceholderValues.SlideImage, null, master.SlideBox, [], 0));
        tree.Append(Shape(id++, "Notes", P.PlaceholderValues.Body, 1, master.NotesBox, [string.Empty], 0));

        return new P.NotesMaster(
            new P.CommonSlideData(tree),
            ColorMap(),
            new P.NotesStyle(new D.Level1ParagraphProperties(new D.DefaultRunProperties { FontSize = (int)Math.Clamp(double.IsFinite(master.FontSize) ? Math.Round(master.FontSize * 100) : 1200, MinimumFontHundredths, MaximumFontHundredths) })));
    }

    private P.HandoutMaster HandoutMaster(HandoutMaster master)
    {
        uint id = 2;
        var tree = Tree();
        Chrome(tree, ref id, master.Header, master.Footer, master.PageNumber);

        var extension = new P.HandoutMasterExtension { Uri = ExtensionUri };
        extension.AppendChild(new OpenXmlUnknownElement("qo", "handout", Pulse.NamespaceName));
        extension.FirstChild!.SetAttribute(new OpenXmlAttribute("slidesPerPage", string.Empty, master.SlidesPerPage.ToString(CultureInfo.InvariantCulture)));

        return new P.HandoutMaster(new P.CommonSlideData(tree), ColorMap(), new P.HandoutMasterExtensionList(extension));
    }

    private P.NotesSlide NotesSlide(NotesMaster master, string notes)
    {
        uint id = 2;
        var tree = Tree();
        tree.Append(Shape(id++, "Slide Image", P.PlaceholderValues.SlideImage, null, master.SlideBox, [], 0));
        tree.Append(Shape(id, "Notes", P.PlaceholderValues.Body, 1, master.NotesBox,
            notes.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'), 0));
        return new P.NotesSlide(new P.CommonSlideData(tree), new P.ColorMapOverride(new D.MasterColorMapping()));
    }

    private void Chrome(P.ShapeTree tree, ref uint id, string header, string footer, bool pageNumber)
    {
        tree.Append(Shape(id++, "Header", P.PlaceholderValues.Header, null, HeaderBox, [header], 0));
        tree.Append(Shape(id++, "Footer", P.PlaceholderValues.Footer, 4, FooterBox, [footer], 0));
        if (pageNumber) tree.Append(Shape(id++, "Page Number", P.PlaceholderValues.SlideNumber, 5, NumberBox, [], 1));
    }

    private P.Shape Shape(uint id, string name, P.PlaceholderValues type, uint? index, PageBox box, IReadOnlyList<string> lines, int fields)
    {
        var placeholder = new P.PlaceholderShape { Type = type };
        if (index is { } value) placeholder.Index = value;

        var body = new P.TextBody(new D.BodyProperties(), new D.ListStyle());
        foreach (var line in lines)
            body.Append(new D.Paragraph(new D.Run(new D.RunProperties(), new D.Text(line))));
        for (var field = 0; field < fields; field++)
            body.Append(new D.Paragraph(new D.Field(new D.RunProperties(), new D.Text("‹#›")) { Id = "{B6F15528-21DE-4FAA-801E-634DDDAF4B2B}", Type = "slidenum" }));
        if (!body.Elements<D.Paragraph>().Any()) body.Append(new D.Paragraph());

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(placeholder)),
            new P.ShapeProperties(
                new D.Transform2D(
                    new D.Offset { X = (long)Math.Round(box.X * PageWidth), Y = (long)Math.Round(box.Y * PageHeight) },
                    new D.Extents { Cx = (long)Math.Round(box.Width * PageWidth), Cy = (long)Math.Round(box.Height * PageHeight) }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle }),
            body);
    }

    private P.ShapeTree Tree() =>
        new(new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup()));

    private P.ColorMap ColorMap() => new()
    {
        Background1 = D.ColorSchemeIndexValues.Light1,
        Text1 = D.ColorSchemeIndexValues.Dark1,
        Background2 = D.ColorSchemeIndexValues.Light2,
        Text2 = D.ColorSchemeIndexValues.Dark2,
        Accent1 = D.ColorSchemeIndexValues.Accent1,
        Accent2 = D.ColorSchemeIndexValues.Accent2,
        Accent3 = D.ColorSchemeIndexValues.Accent3,
        Accent4 = D.ColorSchemeIndexValues.Accent4,
        Accent5 = D.ColorSchemeIndexValues.Accent5,
        Accent6 = D.ColorSchemeIndexValues.Accent6,
        Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
        FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
    };

    private P.Shape? Placeholder(P.ShapeTree? tree, P.PlaceholderValues type) =>
        tree?.Elements<P.Shape>().FirstOrDefault(shape =>
            shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value == type);

    private PageBox? BoxOf(P.Shape? shape)
    {
        if (shape?.ShapeProperties?.Transform2D is not { Offset: { } offset, Extents: { } extents }) return null;
        return new PageBox(
            (offset.X?.Value ?? 0) / (double)PageWidth,
            (offset.Y?.Value ?? 0) / (double)PageHeight,
            (extents.Cx?.Value ?? 0) / (double)PageWidth,
            (extents.Cy?.Value ?? 0) / (double)PageHeight).Clamped();
    }

    private string TextOf(P.Shape? shape)
    {
        var text = shape?.TextBody is { } body ? string.Concat(body.Descendants<D.Run>().SelectMany(run => run.Elements<D.Text>()).Select(run => run.Text)).Trim() : string.Empty;
        return IsPrompt(text) ? string.Empty : text;
    }

    private bool IsPrompt(string text) =>
        text.Length > 2 && ((text[0] == '<' && text[^1] == '>') || (text[0] == '\u2039' && text[^1] == '\u203A'));

    private int? SlidesPerPage(P.HandoutMaster handout)
    {
        var extension = handout.Descendants<P.HandoutMasterExtension>().FirstOrDefault(ext => ext.Uri == ExtensionUri);
        if (extension is null) return null;
        XElement element;
        try { element = XElement.Parse(extension.InnerXml); }
        catch (System.Xml.XmlException) { return null; }
        return int.TryParse((string?)element.Attribute("slidesPerPage"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null;
    }
}

internal sealed class PrintMastersReader(PptxPrintMasters masters) : IPptxPresentationPartReader
{
    public void Read(PresentationPart part, PulseOffice.Presentations.Presentation deck, SlideIndex index) => masters.Read(part, deck);
}

internal sealed class NotesSlideReader(PptxPrintMasters masters) : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        if (masters.NotesOf(source.Slide) is { } notes) assembly.Slide.Notes = notes;
    }
}
