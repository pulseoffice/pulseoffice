using PulseOffice.Foundation.Colors;
using DocumentFormat.OpenXml;
using System.Globalization;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxFlavour(TextDocumentFormat Format, WordprocessingDocumentType Type)
{
    public static readonly DocxFlavour Document = new(new TextDocumentFormat
    {
        Id = "docx",
        DisplayName = "Word Document",
        Extensions = [".docx"],
        MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    }, WordprocessingDocumentType.Document);

    public static readonly DocxFlavour Template = new(new TextDocumentFormat
    {
        Id = "dotx",
        DisplayName = "Word Template",
        Extensions = [".dotx"],
        MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
    }, WordprocessingDocumentType.Template);

    public static readonly DocxFlavour MacroEnabled = new(new TextDocumentFormat
    {
        Id = "docm",
        DisplayName = "Word Macro-Enabled Document",
        Extensions = [".docm"],
        MimeType = "application/vnd.ms-word.document.macroEnabled.12",
    }, WordprocessingDocumentType.MacroEnabledDocument);
}

public sealed class DocxValues
{
    public string Twips(double points) => ((int)Math.Round(points * 20)).ToString(CultureInfo.InvariantCulture);

    public double? Points(string? twips) =>
        double.TryParse(twips, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value / 20.0 : null;

    public int Bounded(long value, int minimum, int maximum) => (int)Math.Clamp(value, minimum, maximum);

    public string Color(uint argb) => new ArgbColor(argb).ToRgbDigits();

    public uint? Color(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Equals("auto", StringComparison.OrdinalIgnoreCase) || hex.Length != 6) return null;
        return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb) ? 0xFF000000 | rgb : null;
    }

    public int? Integer(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value
        : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && Math.Abs(real) < int.MaxValue ? (int)Math.Round(real, MidpointRounding.AwayFromZero)
        : null;

    public string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    public bool On(DocumentFormat.OpenXml.Wordprocessing.OnOffType? flag) =>
        flag is not null && (flag.Val is null || flag.Val.Value);

    public bool Present(OpenXmlElement? element) =>
        element is not null && element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val").Value is not ("0" or "false" or "off");

    public bool? Flag(DocumentFormat.OpenXml.Wordprocessing.OnOffType? flag) =>
        flag is null ? null : flag.Val is null || flag.Val.Value;
}
