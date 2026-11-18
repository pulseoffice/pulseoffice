using DocumentFormat.OpenXml;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxNoteSettingsCodec
{
    private readonly IReadOnlyList<(string Odf, W.NumberFormatValues Word)> _formats =
    [
        ("1", W.NumberFormatValues.Decimal),
        ("a", W.NumberFormatValues.LowerLetter),
        ("A", W.NumberFormatValues.UpperLetter),
        ("i", W.NumberFormatValues.LowerRoman),
        ("I", W.NumberFormatValues.UpperRoman),
    ];

    public IEnumerable<OpenXmlElement> Write(TextDocument document)
    {
        var notes = document.Notes;
        var footnote = new W.FootnoteDocumentWideProperties(new OpenXmlElement[] { FootnotePosition(notes.FootnoteSettings.Placement) }
            .Concat(Common(notes.FootnoteSettings, NoteKind.Footnote)));
        if (notes.OfKind(NoteKind.Footnote).Any())
            footnote.Append(new W.FootnoteSpecialReference { Id = -1 }, new W.FootnoteSpecialReference { Id = 0 });
        yield return footnote;
        var endnote = new W.EndnoteDocumentWideProperties(new OpenXmlElement[] { new W.EndnotePosition { Val = notes.EndnoteSettings.Placement == NotePlacement.SectionEnd ? W.EndnotePositionValues.SectionEnd : W.EndnotePositionValues.DocumentEnd } }
            .Concat(Common(notes.EndnoteSettings, NoteKind.Endnote)));
        if (notes.OfKind(NoteKind.Endnote).Any())
            endnote.Append(new W.EndnoteSpecialReference { Id = -1 }, new W.EndnoteSpecialReference { Id = 0 });
        yield return endnote;
    }

    public bool Expressible(TextDocument document)
    {
        var notes = document.Notes;
        bool Known(string format) => _formats.Any(entry => entry.Odf == format);
        return notes.FootnoteSettings.ContinuationBackward is null && notes.EndnoteSettings.ContinuationBackward is null
            && (!notes.OfKind(NoteKind.Footnote).Any() || Known(notes.FootnoteSettings.FormatName) || notes.OfKind(NoteKind.Footnote).All(note => note.Citation is not null))
            && (!notes.OfKind(NoteKind.Endnote).Any() || Known(notes.EndnoteSettings.FormatName) && notes.EndnoteSettings.Restart != NoteNumbering.Page);
    }

    public void Read(W.Settings settings, TextDocument document)
    {
        var notes = document.Notes;
        if (settings.GetFirstChild<W.FootnoteDocumentWideProperties>() is { } footnote)
            notes.FootnoteSettings = Read(footnote, notes.FootnoteSettings with { FormatName = "1" });
        if (settings.GetFirstChild<W.EndnoteDocumentWideProperties>() is { } endnote)
            notes.EndnoteSettings = Read(endnote, notes.EndnoteSettings with { FormatName = "i" });
    }

    public void Read(W.SectionProperties? section, TextDocument document)
    {
        if (section is null) return;
        var notes = document.Notes;
        if (section.GetFirstChild<W.FootnoteProperties>() is { } footnote) notes.FootnoteSettings = Read(footnote, notes.FootnoteSettings);
        if (section.GetFirstChild<W.EndnoteProperties>() is { } endnote) notes.EndnoteSettings = Read(endnote, notes.EndnoteSettings);
    }

    private IEnumerable<OpenXmlElement> Common(NoteSettings settings, NoteKind kind)
    {
        yield return new W.NumberingFormat { Val = Word(settings.FormatName) };
        yield return new W.NumberingStart { Val = (ushort)Math.Clamp(settings.StartAt, 0, ushort.MaxValue) };
        yield return new W.NumberingRestart
        {
            Val = settings.Restart switch
            {
                NoteNumbering.Page when kind == NoteKind.Footnote => W.RestartNumberValues.EachPage,
                NoteNumbering.Chapter => W.RestartNumberValues.EachSection,
                _ => W.RestartNumberValues.Continuous,
            },
        };
    }

    private NoteSettings Read(OpenXmlCompositeElement properties, NoteSettings settings)
    {
        if (properties.GetFirstChild<W.FootnotePosition>()?.Val?.InnerText is { } footnote) settings = settings with { Placement = Placement(footnote) };
        if (properties.GetFirstChild<W.EndnotePosition>()?.Val?.Value is { } endnote)
            settings = settings with { Placement = endnote == W.EndnotePositionValues.SectionEnd ? NotePlacement.SectionEnd : NotePlacement.DocumentEnd };
        if (properties.GetFirstChild<W.NumberingFormat>()?.Val?.Value is { } format) settings = settings with { FormatName = Odf(format) };
        if (properties.GetFirstChild<W.NumberingStart>()?.Val?.Value is { } start) settings = settings with { StartAt = start };
        if (properties.GetFirstChild<W.NumberingRestart>()?.Val?.Value is { } restart)
            settings = settings with
            {
                Restart = restart == W.RestartNumberValues.EachPage ? NoteNumbering.Page
                    : restart == W.RestartNumberValues.EachSection ? NoteNumbering.Chapter
                    : NoteNumbering.Document,
            };
        return settings;
    }

    private W.FootnotePosition FootnotePosition(NotePlacement placement)
    {
        var position = new W.FootnotePosition { Val = new EnumValue<W.FootnotePositionValues> { InnerText = placement switch
        {
            NotePlacement.BelowText => "beneathText",
            NotePlacement.SectionEnd => "sectEnd",
            NotePlacement.DocumentEnd => "docEnd",
            _ => "pageBottom",
        } } };
        return position;
    }

    private NotePlacement Placement(string position) => position switch
    {
        "beneathText" => NotePlacement.BelowText,
        "sectEnd" => NotePlacement.SectionEnd,
        "docEnd" => NotePlacement.DocumentEnd,
        _ => NotePlacement.PageBottom,
    };

    private W.NumberFormatValues Word(string odf)
    {
        foreach (var (token, word) in _formats)
            if (token == odf) return word;
        return W.NumberFormatValues.Decimal;
    }

    private string Odf(W.NumberFormatValues word)
    {
        foreach (var (token, known) in _formats)
            if (known == word) return token;
        return "1";
    }
}
