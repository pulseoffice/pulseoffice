using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxRunLanguages(ITextScripts scripts)
{
    public PptxRunLanguages() : this(new TextScripts())
    {
    }

    public (string? Western, string? Asian) Read(string? lang, string? altLang)
    {
        var primary = Usable(lang);
        var alternative = Usable(altLang);
        return scripts.IsEastAsianLanguage(primary)
            ? (scripts.IsEastAsianLanguage(alternative) ? null : alternative, primary)
            : (primary, scripts.IsEastAsianLanguage(alternative) ? alternative : null);
    }

    public (string? Lang, string? AltLang) Write(string? western, string? asian, bool eastAsianText) =>
        eastAsianText && asian is not null ? (asian, western) : (western ?? asian, western is null ? null : asian);

    private string? Usable(string? tag) => tag is { Length: > 0 } && tag != "x-none" ? tag : null;
}

internal sealed class PptxTextDefaults(PptxRunLanguages languages)
{
    public PptxTextDefaults() : this(new PptxRunLanguages())
    {
    }

    public (TextLanguages Languages, ScriptFonts Fonts) Read(PresentationPart part)
    {
        var run = part.Presentation?.DefaultTextStyle?.GetFirstChild<D.DefaultParagraphProperties>()?.GetFirstChild<D.DefaultRunProperties>()
            ?? part.Presentation?.DefaultTextStyle?.GetFirstChild<D.Level1ParagraphProperties>()?.GetFirstChild<D.DefaultRunProperties>();
        var (western, asian) = languages.Read(run?.Language?.Value, run?.AlternativeLanguage?.Value);
        var theme = part.SlideMasterParts.FirstOrDefault()?.ThemePart?.Theme?.ThemeElements?.FontScheme?.MinorFont ?? part.ThemePart?.Theme?.ThemeElements?.FontScheme?.MinorFont;
        return (new TextLanguages(western, asian),
            new ScriptFonts(
                Font(run?.GetFirstChild<D.EastAsianFont>()?.Typeface?.Value) ?? Font(theme?.EastAsianFont?.Typeface?.Value),
                Font(run?.GetFirstChild<D.ComplexScriptFont>()?.Typeface?.Value) ?? Font(theme?.ComplexScriptFont?.Typeface?.Value)));
    }

    public P.DefaultTextStyle? Write(TextLanguages defaults, ScriptFonts fonts)
    {
        if (defaults.Western is null && defaults.Asian is null && fonts.IsEmpty) return null;

        var (lang, altLang) = languages.Write(defaults.Western, defaults.Asian, false);
        var run = new D.DefaultRunProperties();
        if (lang is not null) run.Language = lang;
        if (altLang is not null) run.AlternativeLanguage = altLang;
        if (fonts.Asian is { } asian) run.Append(new D.EastAsianFont { Typeface = asian });
        if (fonts.Complex is { } complex) run.Append(new D.ComplexScriptFont { Typeface = complex });
        return new P.DefaultTextStyle(new D.DefaultParagraphProperties(run));
    }

    private string? Font(string? typeface) => typeface is { Length: > 0 } && !typeface.StartsWith('+') ? typeface : null;
}

internal sealed class DefaultTextStyleWriter(PptxTextDefaults defaults) : IPptxPresentationPartWriter
{
    public void Write(PptxWriteSession session)
    {
        if (defaults.Write(session.Deck.DefaultLanguages, session.Deck.DefaultScriptFonts) is { } style)
            session.Part.Presentation!.Append(style);
    }
}
