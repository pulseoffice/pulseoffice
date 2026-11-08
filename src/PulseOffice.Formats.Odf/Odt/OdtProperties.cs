using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed record CharacterReading(CharacterProperties Properties, double? RelativeSize);

internal sealed class OdtCharacterCodec(OdtMeasure measure, OdtLanguageTag languages) : OdtXml
{
    private readonly TokenMap<TextCase> _cases = new(TextCase.None, "none",
        (TextCase.None, "none"), (TextCase.Upper, "uppercase"), (TextCase.Lower, "lowercase"), (TextCase.Capitalize, "capitalize"));

    private readonly TokenMap<FontRelief> _reliefs = new(FontRelief.None, "none",
        (FontRelief.None, "none"), (FontRelief.Embossed, "embossed"), (FontRelief.Engraved, "engraved"));

    public IEnumerable<XAttribute> Write(CharacterProperties properties)
    {
        if (properties.Font is { } font) yield return new XAttribute(Style + "font-name", font);
        if (properties.FontSize is { } size) yield return new XAttribute(Fo + "font-size", measure.Points(size));
        if (properties.Bold is { } bold) yield return new XAttribute(Fo + "font-weight", bold ? "bold" : "normal");
        if (properties.Italic is { } italic) yield return new XAttribute(Fo + "font-style", italic ? "italic" : "normal");
        foreach (var attribute in Underline(properties)) yield return attribute;
        if (properties.Strikethrough is { } strike) yield return new XAttribute(Style + "text-line-through-style", strike ? "solid" : "none");
        if (properties.Overline is { } overline) yield return new XAttribute(Style + "text-overline-style", overline ? "solid" : "none");
        if (properties.Shadow is { } shadow) yield return new XAttribute(Fo + "text-shadow", shadow ? "1pt 1pt" : "none");
        if (properties.Outline is { } outline) yield return new XAttribute(Style + "text-outline", outline ? "true" : "false");
        if (properties.SmallCaps is { } caps) yield return new XAttribute(Fo + "font-variant", caps ? "small-caps" : "normal");
        if (properties.Baseline is { } baseline)
            yield return new XAttribute(Style + "text-position", baseline switch
            {
                TextBaseline.Superscript => "super 58%",
                TextBaseline.Subscript => "sub 58%",
                _ => "0% 100%",
            });
        if (properties.Color is { } color) yield return new XAttribute(Fo + "color", measure.Hex(color));
        if (properties.Highlight is { } highlight) yield return new XAttribute(Fo + "background-color", measure.Hex(highlight));
        if (properties.Language is { } language)
            foreach (var attribute in languages.Attributes(language)) yield return attribute;
        if (properties.LetterSpacing is { } spacing) yield return new XAttribute(Fo + "letter-spacing", spacing == 0 ? "normal" : measure.Points(spacing));
        if (properties.ScaleWidth is { } scale) yield return new XAttribute(Style + "text-scale", measure.Percent(scale / 100));
        if (properties.Kerning is { } kerning) yield return new XAttribute(Style + "letter-kerning", kerning ? "true" : "false");
        if (properties.Case is { } textCase) yield return new XAttribute(Fo + "text-transform", _cases.Token(textCase));
        if (properties.Relief is { } relief) yield return new XAttribute(Style + "font-relief", _reliefs.Token(relief));
        if (properties.Hidden is { } hidden) yield return new XAttribute(Text + "display", hidden ? "none" : "true");
        if (properties.RightToLeft is { } rightToLeft) yield return new XAttribute(Pt + "right-to-left", rightToLeft ? "true" : "false");
    }

    private IEnumerable<XAttribute> Underline(CharacterProperties properties)
    {
        if (properties.DoubleUnderline == true || properties.Underline == true)
        {
            yield return new XAttribute(Style + "text-underline-style", "solid");
            yield return new XAttribute(Style + "text-underline-width", "auto");
            yield return new XAttribute(Style + "text-underline-color", "font-color");
            if (properties.DoubleUnderline == true) yield return new XAttribute(Style + "text-underline-type", "double");
        }
        else if (properties.Underline == false || properties.DoubleUnderline == false)
        {
            yield return new XAttribute(Style + "text-underline-style", "none");
        }
    }

    public CharacterReading Read(XElement? properties, IReadOnlyDictionary<string, string> fontFaces)
    {
        if (properties is null) return new CharacterReading(CharacterProperties.None, null);

        var sizeText = (string?)properties.Attribute(Fo + "font-size");
        var relative = measure.Percent(sizeText);
        var underlineStyle = (string?)properties.Attribute(Style + "text-underline-style");
        var underlineType = (string?)properties.Attribute(Style + "text-underline-type");
        var underlined = underlineStyle is null ? (bool?)null : underlineStyle != "none" && underlineType != "none";
        var doubled = underlined == true && underlineType == "double";

        return new CharacterReading(new CharacterProperties
        {
            Font = Font(properties, fontFaces),
            FontSize = relative is null ? measure.Points(sizeText) : null,
            Bold = Weight((string?)properties.Attribute(Fo + "font-weight")),
            Italic = (string?)properties.Attribute(Fo + "font-style") is { } style ? style is "italic" or "oblique" : null,
            Underline = underlined is null ? null : underlined.Value && !doubled ? true : underlined.Value ? null : false,
            DoubleUnderline = underlined is null ? null : doubled ? true : underlined.Value ? null : false,
            Strikethrough = Line((string?)properties.Attribute(Style + "text-line-through-style"), (string?)properties.Attribute(Style + "text-line-through-type")),
            Overline = Line((string?)properties.Attribute(Style + "text-overline-style"), null),
            Shadow = (string?)properties.Attribute(Fo + "text-shadow") is { } shadow ? shadow != "none" : null,
            Outline = (string?)properties.Attribute(Style + "text-outline") is { } outline ? outline == "true" : null,
            SmallCaps = (string?)properties.Attribute(Fo + "font-variant") is { } variant ? variant == "small-caps" : null,
            Baseline = Position((string?)properties.Attribute(Style + "text-position")),
            Color = measure.Color((string?)properties.Attribute(Fo + "color")),
            Highlight = measure.Color((string?)properties.Attribute(Fo + "background-color")),
            Language = properties.Attribute(Fo + "language") is null ? null : languages.Read(properties),
            LetterSpacing = (string?)properties.Attribute(Fo + "letter-spacing") is { } spacing ? spacing == "normal" ? 0 : measure.Points(spacing) : null,
            ScaleWidth = measure.Percent((string?)properties.Attribute(Style + "text-scale")) is { } scale ? scale * 100 : null,
            Kerning = (string?)properties.Attribute(Style + "letter-kerning") is { } kerning ? kerning == "true" : null,
            Case = (string?)properties.Attribute(Fo + "text-transform") is { } transform ? _cases.Value(transform) : null,
            Relief = (string?)properties.Attribute(Style + "font-relief") is { } relief ? _reliefs.Value(relief) : null,
            Hidden = (string?)properties.Attribute(Text + "display") is { } display ? display == "none" : null,
            RightToLeft = (string?)properties.Attribute(Pt + "right-to-left") is { } rightToLeft ? rightToLeft == "true" : null,
        }, relative);
    }

    private string? Font(XElement properties, IReadOnlyDictionary<string, string> fontFaces)
    {
        if ((string?)properties.Attribute(Style + "font-name") is { Length: > 0 } name)
            return fontFaces.TryGetValue(name, out var family) ? family : name;
        return (string?)properties.Attribute(Fo + "font-family") is { Length: > 0 } direct ? direct.Trim('\'', '"') : null;
    }

    private bool? Weight(string? value) => value switch
    {
        null => null,
        "bold" or "bolder" => true,
        "normal" or "lighter" => false,
        _ => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight) ? weight >= 600 : null,
    };

    private bool? Line(string? style, string? type) =>
        style is null ? null : style != "none" && type != "none";

    private TextBaseline? Position(string? value)
    {
        if (value is null) return null;
        var first = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "0%";
        if (first == "super") return TextBaseline.Superscript;
        if (first == "sub") return TextBaseline.Subscript;
        var percent = measure.Percent(first) ?? 0;
        return percent > 0 ? TextBaseline.Superscript : percent < 0 ? TextBaseline.Subscript : TextBaseline.Normal;
    }
}

internal sealed class OdtParagraphCodec(OdtMeasure measure, OdtBorders borders, OdtStyleNames names) : OdtXml
{
    private readonly TokenMap<TextAlign> _align = new(TextAlign.Left, "start",
        (TextAlign.Left, "start"), (TextAlign.Center, "center"), (TextAlign.Right, "end"), (TextAlign.Justify, "justify"));

    private const int DropCapLimit = 255;

    private readonly PulseOffice.Formats.Odf.TextCodecs.OdfTabStops _tabs = new();

    public XElement? Paragraph(ParagraphProperties properties)
    {
        var element = new XElement(Style + "paragraph-properties");
        if (properties.Align is { } align) element.Add(new XAttribute(Fo + "text-align", _align.Token(align)));
        WriteLineHeight(element, properties);
        if (properties.SpaceBefore is { } before) element.Add(new XAttribute(Fo + "margin-top", measure.Points(before)));
        if (properties.SpaceAfter is { } after) element.Add(new XAttribute(Fo + "margin-bottom", measure.Points(after)));
        var mirrored = properties.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        if ((mirrored ? properties.MarginRight : properties.MarginLeft) is { } left) element.Add(new XAttribute(Fo + "margin-left", measure.Points(left)));
        if ((mirrored ? properties.MarginLeft : properties.MarginRight) is { } right) element.Add(new XAttribute(Fo + "margin-right", measure.Points(right)));
        if (properties.Direction is { } direction) element.Add(new XAttribute(Style + "writing-mode", mirrored ? "rl-tb" : "lr-tb"));
        if (properties.Indent is { } indent) element.Add(new XAttribute(Fo + "text-indent", measure.Points(indent)));
        if (properties.KeepWithNext is { } next) element.Add(new XAttribute(Fo + "keep-with-next", next ? "always" : "auto"));
        if (properties.KeepTogether is { } together) element.Add(new XAttribute(Fo + "keep-together", together ? "always" : "auto"));
        if (properties.Widows is { } widows) element.Add(new XAttribute(Fo + "widows", widows));
        if (properties.Orphans is { } orphans) element.Add(new XAttribute(Fo + "orphans", orphans));
        if ((properties.PageBreakBefore, properties.ColumnBreakBefore) switch
            {
                (true, _) => "page",
                (_, true) => "column",
                (false, _) => "auto",
                _ => null,
            } is { } breakBefore) element.Add(new XAttribute(Fo + "break-before", breakBefore));
        if (properties.PageNumberRestart is { } restart) element.Add(new XAttribute(Style + "page-number", restart));
        if (properties.ContextualSpacing is { } contextual) element.Add(new XAttribute(Style + "contextual-spacing", contextual ? "true" : "false"));
        if (properties.Background is { } background) element.Add(new XAttribute(Fo + "background-color", measure.Fill(background)));
        if (properties.Borders is { } box) borders.Write(element, box);
        if (properties.Hyphenation is { } hyphenation)
            element.Add(new XAttribute(Fo + "hyphenation-ladder-count", hyphenation.MaximumConsecutive <= 0 ? "no-limit" : hyphenation.MaximumConsecutive.ToString(CultureInfo.InvariantCulture)));
        if (properties.TabStops is { } stops) element.Add(_tabs.Write(stops));
        if (properties.DropCap is { } drop) element.Add(DropCap(drop));
        return element.HasAttributes || element.HasElements ? element : null;
    }

    public IEnumerable<XAttribute> Hyphenation(ParagraphProperties properties)
    {
        if (properties.Hyphenation is not { } hyphenation) yield break;
        yield return new XAttribute(Fo + "hyphenate", hyphenation.Enabled ? "true" : "false");
        yield return new XAttribute(Fo + "hyphenation-remain-char-count", hyphenation.CharactersBefore);
        yield return new XAttribute(Fo + "hyphenation-push-char-count", hyphenation.CharactersAfter);
        yield return new XAttribute(Loext + "hyphenation-no-caps", hyphenation.SkipCapitalized ? "true" : "false");
        yield return new XAttribute(Loext + "hyphenation-word-char-count", hyphenation.MinimumWordLength);
    }

    private void WriteLineHeight(XElement element, ParagraphProperties properties)
    {
        var proportional = properties.LineHeight is { Mode: LineHeightMode.Proportional };
        var exact = properties.LineHeight is { Mode: LineHeightMode.Exact };
        if (properties.LineSpacing is { } spacing)
        {
            if (proportional || exact) element.Add(new XAttribute(Pt + "line-spacing", spacing.ToString("0.####", CultureInfo.InvariantCulture)));
            else element.Add(new XAttribute(Fo + "line-height", measure.Percent(spacing)));
        }

        switch (properties.LineHeight)
        {
            case { Mode: LineHeightMode.Proportional } line:
                element.Add(new XAttribute(Fo + "line-height", measure.Percent(line.Value)));
                element.Add(new XAttribute(Pt + "line-height-mode", "proportional"));
                break;
            case { Mode: LineHeightMode.Exact } line:
                element.Add(new XAttribute(Fo + "line-height", measure.Points(line.Value)));
                break;
            case { Mode: LineHeightMode.AtLeast } line:
                element.Add(new XAttribute(Style + "line-height-at-least", measure.Points(line.Value)));
                break;
            case { Mode: LineHeightMode.Leading } line:
                element.Add(new XAttribute(Style + "line-spacing", measure.Points(line.Value)));
                break;
        }
    }

    private XElement DropCap(DropCap drop)
    {
        var element = new XElement(Style + "drop-cap",
            new XAttribute(Style + "lines", drop.IsActive ? drop.Lines : 1),
            new XAttribute(Style + "length", drop.WholeWord ? "word" : drop.Characters.ToString(CultureInfo.InvariantCulture)),
            new XAttribute(Style + "distance", measure.Points(drop.Distance)));
        if (drop.CharacterStyleName is { } style) element.Add(new XAttribute(Style + "style-name", names.Encode(style)));
        return element;
    }

    private DropCap? ReadDropCap(XElement? element)
    {
        if (element is null) return null;
        var length = (string?)element.Attribute(Style + "length");
        return new DropCap(Math.Clamp(measure.Integer((string?)element.Attribute(Style + "lines")) ?? 1, 1, DropCapLimit),
            length == "word" ? 0 : Math.Clamp(measure.Integer(length) ?? 1, 0, DropCapLimit))
        {
            WholeWord = length == "word",
            Distance = measure.Points((string?)element.Attribute(Style + "distance")) ?? 0,
            CharacterStyleName = (string?)element.Attribute(Style + "style-name") is { } style ? names.Decode(style) : null,
        };
    }

    public ParagraphProperties Read(XElement? paragraph, XElement? text)
    {
        if (paragraph is null && text is null) return ParagraphProperties.None;
        var p = paragraph ?? new XElement(Style + "paragraph-properties");
        var lineText = (string?)p.Attribute(Fo + "line-height");
        var percent = measure.Percent(lineText);
        var pulseSpacing = (string?)p.Attribute(Pt + "line-spacing") is { } stored
            && double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : (double?)null;
        var proportionalMode = (string?)p.Attribute(Pt + "line-height-mode") == "proportional";

        ParagraphLineHeight? lineHeight = null;
        double? lineSpacing = pulseSpacing;
        if (percent is { } factor)
        {
            if (proportionalMode) lineHeight = ParagraphLineHeight.Proportional(factor);
            else lineSpacing = factor;
        }
        else if (measure.Points(lineText) is { } exact) lineHeight = ParagraphLineHeight.Exact(exact);
        if (measure.Points((string?)p.Attribute(Style + "line-height-at-least")) is { } atLeast) lineHeight = ParagraphLineHeight.AtLeast(atLeast);
        if (measure.Points((string?)p.Attribute(Style + "line-spacing")) is { } leading) lineHeight = ParagraphLineHeight.Leading(leading);

        var direction = DirectionOf((string?)p.Attribute(Style + "writing-mode"));
        var mirrored = direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        var physicalLeft = measure.Points((string?)p.Attribute(Fo + "margin-left"));
        var physicalRight = measure.Points((string?)p.Attribute(Fo + "margin-right"));
        return new ParagraphProperties
        {
            Direction = direction,
            Align = (string?)p.Attribute(Fo + "text-align") is { } align ? AlignOf(align, mirrored) : null,
            LineSpacing = lineSpacing,
            LineHeight = lineHeight,
            SpaceBefore = measure.Points((string?)p.Attribute(Fo + "margin-top")),
            SpaceAfter = measure.Points((string?)p.Attribute(Fo + "margin-bottom")),
            MarginLeft = mirrored ? physicalRight : physicalLeft,
            MarginRight = mirrored ? physicalLeft : physicalRight,
            Indent = measure.Points((string?)p.Attribute(Fo + "text-indent")),
            KeepWithNext = (string?)p.Attribute(Fo + "keep-with-next") is { } next ? next == "always" : null,
            KeepTogether = (string?)p.Attribute(Fo + "keep-together") is { } together ? together == "always" : null,
            Widows = measure.Integer((string?)p.Attribute(Fo + "widows")),
            Orphans = measure.Integer((string?)p.Attribute(Fo + "orphans")),
            PageBreakBefore = (string?)p.Attribute(Fo + "break-before") switch { "page" => true, "auto" => false, _ => null },
            ColumnBreakBefore = (string?)p.Attribute(Fo + "break-before") == "column" ? true : null,
            PageNumberRestart = measure.Integer((string?)p.Attribute(Style + "page-number")),
            ContextualSpacing = (string?)p.Attribute(Style + "contextual-spacing") is { } contextual ? contextual == "true" : null,
            Background = measure.Fill((string?)p.Attribute(Fo + "background-color")),
            Borders = borders.Read(p),
            DropCap = ReadDropCap(p.Element(Style + "drop-cap")),
            TabStops = _tabs.Read(p, measure.Points),
            Hyphenation = ReadHyphenation(p, text),
        };
    }

    private TextAlign? AlignOf(string token, bool mirrored) => token switch
    {
        "left" => mirrored ? TextAlign.Right : TextAlign.Left,
        "right" => mirrored ? TextAlign.Left : TextAlign.Right,
        _ => _align.Find(token),
    };

    private PulseOffice.Text.Rich.Bidi.TextDirection? DirectionOf(string? mode) => mode switch
    {
        "rl-tb" or "rl" => PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft,
        "lr-tb" or "lr" => PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight,
        _ => null,
    };

    private ParagraphHyphenation? ReadHyphenation(XElement paragraph, XElement? text)
    {
        var hyphenate = (string?)text?.Attribute(Fo + "hyphenate");
        var ladder = (string?)paragraph.Attribute(Fo + "hyphenation-ladder-count");
        if (hyphenate is null && ladder is null) return null;
        var defaults = ParagraphHyphenation.On;
        return new ParagraphHyphenation
        {
            Enabled = hyphenate is null ? defaults.Enabled : hyphenate == "true",
            CharactersBefore = measure.Integer((string?)text?.Attribute(Fo + "hyphenation-remain-char-count")) ?? defaults.CharactersBefore,
            CharactersAfter = measure.Integer((string?)text?.Attribute(Fo + "hyphenation-push-char-count")) ?? defaults.CharactersAfter,
            SkipCapitalized = (string?)text?.Attribute(Loext + "hyphenation-no-caps") == "true",
            MinimumWordLength = measure.Integer((string?)text?.Attribute(Loext + "hyphenation-word-char-count")) ?? defaults.MinimumWordLength,
            MaximumConsecutive = measure.Integer(ladder) ?? 0,
        };
    }
}

internal sealed class OdtFrameCodec(OdtMeasure measure) : OdtXml
{
    private readonly TokenMap<WrapMode> _wrap = new(WrapMode.Parallel, "parallel",
        (WrapMode.None, "none"), (WrapMode.Parallel, "parallel"), (WrapMode.Left, "left"), (WrapMode.Right, "right"),
        (WrapMode.Optimal, "dynamic"), (WrapMode.Through, "run-through"), (WrapMode.TopAndBottom, "none"));

    private readonly TokenMap<HorizontalRelation> _horizontal = new(HorizontalRelation.Paragraph, "paragraph",
        (HorizontalRelation.Paragraph, "paragraph"), (HorizontalRelation.PageArea, "page-content"), (HorizontalRelation.Page, "page"),
        (HorizontalRelation.Margin, "page-start-margin"), (HorizontalRelation.Character, "char"), (HorizontalRelation.Column, "paragraph-content"));

    private readonly TokenMap<VerticalRelation> _vertical = new(VerticalRelation.Paragraph, "paragraph",
        (VerticalRelation.Paragraph, "paragraph"), (VerticalRelation.PageArea, "page-content"), (VerticalRelation.Page, "page"),
        (VerticalRelation.Margin, "paragraph-content"), (VerticalRelation.Line, "line"), (VerticalRelation.Character, "char"));

    public string Wrap(WrapMode mode) => _wrap.Token(mode);

    public WrapMode Wrap(string? token, bool topAndBottom) => token == "none" && topAndBottom ? WrapMode.TopAndBottom : _wrap.Value(token);

    public string Horizontal(HorizontalRelation relation) => _horizontal.Token(relation);

    public HorizontalRelation Horizontal(string? token) => _horizontal.Value(token);

    public string Vertical(VerticalRelation relation) => _vertical.Token(relation);

    public VerticalRelation Vertical(string? token) => _vertical.Value(token);

    public XElement Graphic(FrameProperties properties)
    {
        var element = new XElement(Style + "graphic-properties");
        if (properties.Wrap is { } wrap)
        {
            element.Add(new XAttribute(Style + "wrap", Wrap(wrap)));
            if (wrap == WrapMode.TopAndBottom) element.Add(new XAttribute(Pt + "wrap", "top-and-bottom"));
        }
        if (properties.Background is { } background) element.Add(new XAttribute(Fo + "background-color", measure.Hex(background)));
        if (properties.BorderWidth is not null || properties.BorderColor is not null)
            element.Add(new XAttribute(Fo + "border", $"{measure.Points(properties.BorderWidth ?? 0.5)} solid {measure.Hex(properties.BorderColor ?? 0xFF000000)}"));
        if (properties.Padding is { } padding) element.Add(new XAttribute(Fo + "padding", measure.Points(padding)));
        if (properties.Horizontal is { } horizontal) element.Add(new XAttribute(Style + "horizontal-rel", Horizontal(horizontal)));
        if (properties.Vertical is { } vertical) element.Add(new XAttribute(Style + "vertical-rel", Vertical(vertical)));
        return element;
    }

    public void WriteLook(XElement graphic, FrameProperties look)
    {
        if (look.Background is { } background)
        {
            graphic.SetAttributeValue(Draw + "fill", "solid");
            graphic.SetAttributeValue(Draw + "fill-color", measure.Hex(background));
            graphic.SetAttributeValue(Fo + "background-color", measure.Hex(background));
        }
        if (look.BorderWidth is not null || look.BorderColor is not null)
            graphic.SetAttributeValue(Fo + "border", $"{measure.Points(look.BorderWidth ?? 0.5)} solid {measure.Hex(look.BorderColor ?? 0xFF000000)}");
    }

    public FrameProperties? ReadLook(XElement? graphic)
    {
        if (graphic is null) return null;
        var read = Read(graphic);
        var fill = (string?)graphic.Attribute(Draw + "fill");
        var background = fill == "none" ? null : read.Background ?? (fill == "solid" ? measure.Color((string?)graphic.Attribute(Draw + "fill-color")) : null);
        var bordered = read.BorderWidth is > 0;
        return background is null && !bordered ? null : new FrameProperties
        {
            Background = background,
            BorderWidth = bordered ? read.BorderWidth : null,
            BorderColor = bordered ? read.BorderColor : null,
        };
    }

    public FrameProperties Read(XElement? graphic)
    {
        if (graphic is null) return FrameProperties.None;
        var border = (string?)graphic.Attribute(Fo + "border");
        var parts = border is null or "none" ? [] : border.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new FrameProperties
        {
            Wrap = (string?)graphic.Attribute(Style + "wrap") is { } wrap ? Wrap(wrap, (string?)graphic.Attribute(Pt + "wrap") == "top-and-bottom") : null,
            Background = measure.Color((string?)graphic.Attribute(Fo + "background-color")),
            BorderWidth = parts.Select(part => measure.Points(part)).FirstOrDefault(points => points is not null),
            BorderColor = parts.Select(part => measure.Color(part)).FirstOrDefault(c => c is not null),
            Padding = measure.Points((string?)graphic.Attribute(Fo + "padding")),
            Horizontal = (string?)graphic.Attribute(Style + "horizontal-rel") is { } horizontal ? Horizontal(horizontal) : null,
            Vertical = (string?)graphic.Attribute(Style + "vertical-rel") is { } vertical ? Vertical(vertical) : null,
        };
    }
}

internal sealed class OdtBorders(OdtMeasure measure) : OdtXml
{
    private readonly TokenMap<BorderLineStyle> _styles = new(BorderLineStyle.Solid, "solid",
        (BorderLineStyle.Solid, "solid"), (BorderLineStyle.Dotted, "dotted"), (BorderLineStyle.Dashed, "dashed"), (BorderLineStyle.Double, "double"));

    private readonly IReadOnlyList<(BorderSide Side, string Name)> _sides =
        [(BorderSide.Top, "top"), (BorderSide.Bottom, "bottom"), (BorderSide.Left, "left"), (BorderSide.Right, "right")];

    public void Write(XElement element, BoxBorders borders)
    {
        if (borders.IsUniform)
        {
            element.Add(new XAttribute(Fo + "border", Line(borders.Top)));
            if (borders.Top is { Style: BorderLineStyle.Double } line) element.Add(new XAttribute(Style + "border-line-width", DoubleWidths(line)));
            if (borders.PaddingTop > 0 || borders.HasLines) element.Add(new XAttribute(Fo + "padding", measure.Points(borders.PaddingTop)));
            return;
        }
        foreach (var (side, name) in _sides)
        {
            element.Add(new XAttribute(Fo + $"border-{name}", Line(borders.Line(side))));
            if (borders.Line(side) is { Style: BorderLineStyle.Double } line) element.Add(new XAttribute(Style + $"border-line-width-{name}", DoubleWidths(line)));
            element.Add(new XAttribute(Fo + $"padding-{name}", measure.Points(borders.Padding(side))));
        }
    }

    public BoxBorders? Read(XElement element)
    {
        var all = (string?)element.Attribute(Fo + "border");
        var padding = measure.Points((string?)element.Attribute(Fo + "padding"));
        var found = all is not null || _sides.Any(side => element.Attribute(Fo + $"border-{side.Name}") is not null);
        if (!found) return null;
        var borders = new BoxBorders();
        foreach (var (side, name) in _sides)
        {
            var line = Line((string?)element.Attribute(Fo + $"border-{name}") ?? all);
            var space = measure.Points((string?)element.Attribute(Fo + $"padding-{name}")) ?? padding ?? 0;
            borders = borders.With(side, line, space);
        }
        return borders;
    }

    private string Line(BorderLine? line) =>
        line is null || line.Width <= 0 ? "none" : $"{measure.Points(line.Width)} {_styles.Token(line.Style)} {measure.Hex(line.Color)}";

    private string DoubleWidths(BorderLine line)
    {
        var third = measure.Points(line.Width / 3);
        return $"{third} {third} {third}";
    }

    private BorderLine? Line(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "none") return null;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var width = parts.Select(part => measure.Points(part)).FirstOrDefault(points => points is not null);
        if (width is null)
        {
            width = parts.FirstOrDefault() switch
            {
                "thin" => 0.75,
                "medium" => 2.25,
                "thick" => 4.5,
                _ => null,
            };
        }
        var style = parts.Select(part => _styles.Find(part)).FirstOrDefault(found => found is not null)
            ?? (parts.Contains("groove") || parts.Contains("ridge") ? BorderLineStyle.Double : BorderLineStyle.Solid);
        if (parts.Contains("hidden") || width is not > 0) return null;
        var color = parts.Select(part => measure.Color(part)).FirstOrDefault(c => c is not null) ?? 0xFF000000;
        return new BorderLine(width.Value, color) { Style = style };
    }
}
