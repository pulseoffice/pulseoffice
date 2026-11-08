using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

public interface IDrawingNumberSchemes
{
    ListNumberFormat? Read(D.AutoNumberedBullet? bullet);

    D.AutoNumberedBullet Write(ListNumberFormat? format);

    bool Reaches(ListNumberFormat? format);
}

public sealed class DrawingNumberSchemes(IListNumberFormats formats) : IDrawingNumberSchemes
{
    private const string Fallback = "arabicPeriod";

    private readonly IReadOnlyList<SchemeRow> _exact =
    [
        new("arabicPeriod", ListNumberStyle.Arabic, "", "."),
        new("arabicParenR", ListNumberStyle.Arabic, "", ")"),
        new("arabicParenBoth", ListNumberStyle.Arabic, "(", ")"),
        new("arabicPlain", ListNumberStyle.Arabic, "", ""),
        new("alphaLcPeriod", ListNumberStyle.LowerLetter, "", "."),
        new("alphaLcParenR", ListNumberStyle.LowerLetter, "", ")"),
        new("alphaLcParenBoth", ListNumberStyle.LowerLetter, "(", ")"),
        new("alphaUcPeriod", ListNumberStyle.UpperLetter, "", "."),
        new("alphaUcParenR", ListNumberStyle.UpperLetter, "", ")"),
        new("alphaUcParenBoth", ListNumberStyle.UpperLetter, "(", ")"),
        new("romanLcPeriod", ListNumberStyle.LowerRoman, "", "."),
        new("romanLcParenR", ListNumberStyle.LowerRoman, "", ")"),
        new("romanLcParenBoth", ListNumberStyle.LowerRoman, "(", ")"),
        new("romanUcPeriod", ListNumberStyle.UpperRoman, "", "."),
        new("romanUcParenR", ListNumberStyle.UpperRoman, "", ")"),
        new("romanUcParenBoth", ListNumberStyle.UpperRoman, "(", ")"),
    ];

    private readonly IReadOnlyList<SchemeRow> _native =
    [
        new("arabicDbPeriod", ListNumberStyle.Arabic, "", "."),
        new("arabicDbPlain", ListNumberStyle.Arabic, "", ""),
        new("arabic1Minus", ListNumberStyle.Arabic, "", "-"),
        new("arabic2Minus", ListNumberStyle.Arabic, "-", "-"),
        new("circleNumDbPlain", ListNumberStyle.Arabic, "", ""),
        new("circleNumWdBlackPlain", ListNumberStyle.Arabic, "", ""),
        new("circleNumWdWhitePlain", ListNumberStyle.Arabic, "", ""),
        new("ea1ChsPeriod", ListNumberStyle.Arabic, "", "."),
        new("ea1ChsPlain", ListNumberStyle.Arabic, "", ""),
        new("ea1ChtPeriod", ListNumberStyle.Arabic, "", "."),
        new("ea1ChtPlain", ListNumberStyle.Arabic, "", ""),
        new("ea1JpnChsDbPeriod", ListNumberStyle.Arabic, "", "."),
        new("ea1JpnKorPeriod", ListNumberStyle.Arabic, "", "."),
        new("ea1JpnKorPlain", ListNumberStyle.Arabic, "", ""),
        new("hebrew2Minus", ListNumberStyle.Arabic, "", "-"),
        new("hindiAlphaPeriod", ListNumberStyle.LowerLetter, "", "."),
        new("hindiAlpha1Period", ListNumberStyle.LowerLetter, "", "."),
        new("hindiNumPeriod", ListNumberStyle.Arabic, "", "."),
        new("hindiNumParenR", ListNumberStyle.Arabic, "", ")"),
        new("thaiAlphaPeriod", ListNumberStyle.LowerLetter, "", "."),
        new("thaiAlphaParenR", ListNumberStyle.LowerLetter, "", ")"),
        new("thaiAlphaParenBoth", ListNumberStyle.LowerLetter, "(", ")"),
        new("thaiNumPeriod", ListNumberStyle.Arabic, "", "."),
        new("thaiNumParenR", ListNumberStyle.Arabic, "", ")"),
        new("thaiNumParenBoth", ListNumberStyle.Arabic, "(", ")"),
    ];

    public DrawingNumberSchemes() : this(new ListNumberFormats())
    {
    }

    public ListNumberFormat? Read(D.AutoNumberedBullet? bullet)
    {
        if (bullet is null) return null;
        var scheme = Attribute(bullet, "type");
        var start = int.TryParse(Attribute(bullet, "startAt"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 1;
        var exact = _exact.FirstOrDefault(row => row.Scheme == scheme);
        var native = exact is null ? _native.FirstOrDefault(row => row.Scheme == scheme) : null;
        var row = exact ?? native ?? _exact[0];
        return formats.Normalized(new ListNumberFormat
        {
            Style = row.Style,
            Prefix = row.Prefix,
            Suffix = row.Suffix,
            Start = start,
            DrawingScheme = native?.Scheme,
        });
    }

    public D.AutoNumberedBullet Write(ListNumberFormat? format)
    {
        var used = formats.Normalized(format) ?? new ListNumberFormat();
        var scheme = Native(used) ?? Exact(used)?.Scheme ?? Nearest(used);
        return new D.AutoNumberedBullet
        {
            Type = new D.TextAutoNumberSchemeValues(scheme),
            StartAt = used.Start > 1 ? used.Start : null,
        };
    }

    public bool Reaches(ListNumberFormat? format)
    {
        var used = formats.Normalized(format);
        return used is null || Native(used) is not null || used.OdfFormat is null && Exact(used) is not null;
    }

    private string? Native(ListNumberFormat format) =>
        format.DrawingScheme is { } scheme && _native.Concat(_exact).Any(row => row.Scheme == scheme) ? scheme : null;

    private SchemeRow? Exact(ListNumberFormat format) =>
        _exact.FirstOrDefault(row => row.Style == format.Style && row.Prefix == format.Prefix && row.Suffix == format.Suffix);

    private string Nearest(ListNumberFormat format)
    {
        var closing = format.Suffix.Contains(')', StringComparison.Ordinal);
        var opening = format.Prefix.Contains('(', StringComparison.Ordinal);
        var suffix = closing && opening ? ")" : closing ? ")" : format.Suffix.Length == 0 && format.Prefix.Length == 0 ? "" : ".";
        var prefix = closing && opening ? "(" : "";
        return _exact.FirstOrDefault(row => row.Style == format.Style && row.Prefix == prefix && row.Suffix == suffix)?.Scheme
            ?? _exact.FirstOrDefault(row => row.Style == format.Style && row.Suffix == ".")?.Scheme
            ?? Fallback;
    }

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name && string.IsNullOrEmpty(attribute.NamespaceUri)).Value;

    private sealed record SchemeRow(string Scheme, ListNumberStyle Style, string Prefix, string Suffix);
}
