using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Shapes;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlFontworkText(XlsxShapeStyleMapper styles)
{
    private readonly FontworkWarps _warps = new();

    public D.BodyProperties Body(FontworkObject fontwork)
    {
        var body = new D.BodyProperties(new D.PresetTextWarp(new D.AdjustValueList()) { Preset = new EnumValue<D.TextShapeValues>(new D.TextShapeValues(_warps.Ooxml(fontwork.Warp))) })
        {
            Wrap = D.TextWrappingValues.None,
        };
        body.Append(new D.NormalAutoFit());
        return body;
    }

    public D.Paragraph Paragraph(FontworkObject fontwork)
    {
        var run = new D.RunProperties { Bold = fontwork.Bold, Italic = fontwork.Italic, FontSize = 4400 };
        if (fontwork.Style.Line.IsVisible) run.Append(styles.Line(fontwork.Style.Line));
        run.Append(styles.Fill(fontwork.Style.Fill));
        run.Append(new D.LatinFont { Typeface = fontwork.FontFamily });
        return new D.Paragraph(new D.ParagraphProperties { Alignment = D.TextAlignmentTypeValues.Center }, new D.Run(run, new D.Text(fontwork.Text)));
    }

    public bool Reads(OpenXmlElement? textBody) =>
        textBody?.GetFirstChild<D.BodyProperties>()?.GetFirstChild<D.PresetTextWarp>()?.Preset?.InnerText is { } preset && preset != "textNoShape";

    public FontworkObject Read(OpenXmlElement textBody, string id)
    {
        var warp = textBody.GetFirstChild<D.BodyProperties>()?.GetFirstChild<D.PresetTextWarp>()?.Preset?.InnerText;
        var run = textBody.Descendants<D.RunProperties>().FirstOrDefault();
        var fill = run?.GetFirstChild<D.SolidFill>()?.RgbColorModelHex?.Val?.Value;
        var line = run?.GetFirstChild<D.Outline>();
        return new FontworkObject
        {
            Id = id,
            Text = string.Join(" ", textBody.Descendants<D.Paragraph>().Select(paragraph => string.Concat(paragraph.Descendants<D.Text>().Select(text => text.Text)))),
            Warp = _warps.FromOoxml(warp),
            Bold = run?.Bold?.Value ?? true,
            Italic = run?.Italic?.Value ?? false,
            FontFamily = run?.GetFirstChild<D.LatinFont>()?.Typeface?.Value ?? "Inter",
            Style = ShapeStyle.Default with
            {
                Fill = fill is null ? ShapeStyle.Default.Fill : ShapeStyle.Default.Fill with { ColorHex = "#" + fill },
                Line = line?.GetFirstChild<D.SolidFill>()?.RgbColorModelHex?.Val?.Value is { } colour
                    ? ShapeStyle.Default.Line with { ColorHex = "#" + colour }
                    : ShapeLine.None,
            },
        };
    }
}
