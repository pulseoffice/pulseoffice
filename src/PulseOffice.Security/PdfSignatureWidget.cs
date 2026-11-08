using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Security;

public sealed record PdfSignatureAppearance(string SignedBy = "Digitally signed by", string Date = "Date", string Reason = "Reason", string Place = "Location");

internal sealed record PdfWidget(string Entries, IReadOnlyList<PdfObject> Objects);

internal sealed partial class PdfSignatureWidget(IPdfSignatureFont font)
{
    private const int MaximumDepth = 32;
    private const double Margin = 36;
    private const double PreferredWidth = 240;
    private const double FontSize = 8;
    private const double LineHeight = 11;

    [GeneratedRegex(@"/Pages\s+(\d+)\s+\d+\s+R")]
    private partial Regex PagesEntry();

    [GeneratedRegex(@"/Kids\s*\[\s*(\d+)\s+\d+\s+R")]
    private partial Regex FirstKid();

    [GeneratedRegex(@"/Type\s*/Page\b(?!s)")]
    private partial Regex PageType();

    [GeneratedRegex(@"/Parent\s+(\d+)\s+\d+\s+R")]
    private partial Regex ParentEntry();

    [GeneratedRegex(@"/MediaBox\s*\[\s*([-+\d.]+)\s+([-+\d.]+)\s+([-+\d.]+)\s+([-+\d.]+)\s*\]")]
    private partial Regex MediaBoxEntry();

    [GeneratedRegex(@"/Annots\s*\[")]
    private partial Regex InlineAnnotations();

    [GeneratedRegex(@"/Annots\s+(\d+)\s+\d+\s+R")]
    private partial Regex IndirectAnnotations();

    public PdfWidget Invisible() => new("/F 132 /Rect [0 0 0 0]", []);

    public PdfWidget Visible(PdfObjectIndex index, PdfObject catalogue, int widgetId, IPdfAppendedObjects appended, PdfSignatureAppearance labels, string signer, string? reason, string? place, DateTimeOffset when)
    {
        if (FirstPage(index, catalogue.Body) is not { } page) return Invisible();

        var lines = new List<string> { $"{labels.SignedBy} {signer}", $"{labels.Date}: {when.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)}" };
        if (reason is { Length: > 0 }) lines.Add($"{labels.Reason}: {reason}");
        if (place is { Length: > 0 }) lines.Add($"{labels.Place}: {place}");

        var (left, bottom, right, top) = Box(index, page);
        var width = Math.Max(1, Math.Min(PreferredWidth, right - left - 2 * Margin));
        var height = lines.Count * LineHeight + 8;
        var x = left + Math.Min(Margin, Math.Max(0, (right - left - width) / 2));
        var y = bottom + Math.Min(Margin, Math.Max(0, (top - bottom - height) / 2));

        var appearanceId = appended.Reserve();
        appended.Write(appearanceId, Appearance(font.Typeset(lines, FontSize, appended), width, height));
        var objects = WithAnnotation(index, page, widgetId).ToList();

        var rectangle = string.Create(CultureInfo.InvariantCulture, $"[{Number(x)} {Number(y)} {Number(x + width)} {Number(y + height)}]");
        return new PdfWidget($"/F 4 /P {page.Id} 0 R /Rect {rectangle} /AP << /N {appearanceId} 0 R >>", objects);
    }

    private PdfObject? FirstPage(PdfObjectIndex index, string catalogue)
    {
        if (PagesEntry().Match(catalogue) is not { Success: true } pages) return null;
        var id = int.Parse(pages.Groups[1].Value, CultureInfo.InvariantCulture);

        for (var depth = 0; depth < MaximumDepth; depth++)
        {
            if (index.Body(id) is not { } body) return null;
            if (PageType().IsMatch(body)) return new PdfObject(id, body);
            if (FirstKid().Match(body) is not { Success: true } kid) return null;
            id = int.Parse(kid.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    private (double Left, double Bottom, double Right, double Top) Box(PdfObjectIndex index, PdfObject page)
    {
        var body = page.Body;
        for (var depth = 0; depth < MaximumDepth && body is not null; depth++)
        {
            if (MediaBoxEntry().Match(body) is { Success: true } box && Parse(box) is { } parsed) return parsed;
            body = ParentEntry().Match(body) is { Success: true } parent ? index.Body(int.Parse(parent.Groups[1].Value, CultureInfo.InvariantCulture)) : null;
        }

        return (0, 0, 612, 792);
    }

    private (double, double, double, double)? Parse(Match box)
    {
        var values = new double[4];
        for (var i = 0; i < 4; i++)
            if (!double.TryParse(box.Groups[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i])) return null;

        var (left, right) = (Math.Min(values[0], values[2]), Math.Max(values[0], values[2]));
        var (bottom, top) = (Math.Min(values[1], values[3]), Math.Max(values[1], values[3]));
        return right - left >= 1 && top - bottom >= 1 ? (left, bottom, right, top) : null;
    }

    private IEnumerable<PdfObject> WithAnnotation(PdfObjectIndex index, PdfObject page, int widgetId)
    {
        var reference = $" {widgetId} 0 R";
        if (InlineAnnotations().Match(page.Body) is { Success: true } inline)
        {
            var close = page.Body.IndexOf(']', inline.Index + inline.Length);
            if (close >= 0)
            {
                yield return page with { Body = page.Body[..close] + reference + page.Body[close..] };
                yield break;
            }
        }

        if (IndirectAnnotations().Match(page.Body) is { Success: true } indirect)
        {
            var arrayId = int.Parse(indirect.Groups[1].Value, CultureInfo.InvariantCulture);
            if (index.Body(arrayId) is { } array && array.LastIndexOf(']') is var end and >= 0)
            {
                yield return new PdfObject(arrayId, array[..end] + reference + array[end..]);
                yield break;
            }

            yield return page with { Body = page.Body.Remove(indirect.Index, indirect.Length).Insert(indirect.Index, $"/Annots [{reference.Trim()}]") };
            yield break;
        }

        var dictionaryEnd = page.Body.LastIndexOf(">>", StringComparison.Ordinal);
        if (dictionaryEnd >= 0) yield return page with { Body = page.Body[..dictionaryEnd] + $" /Annots [{reference.Trim()}] " + page.Body[dictionaryEnd..] };
    }

    private string Appearance(PdfSignatureText text, double width, double height)
    {
        var content = new StringBuilder();
        content.Append(CultureInfo.InvariantCulture, $"q 0.96 0.97 1 rg 0.15 0.39 0.92 RG 1 w 0.5 0.5 {Number(width - 1)} {Number(height - 1)} re B Q\n");
        content.Append(CultureInfo.InvariantCulture, $"BT 0.1 0.1 0.1 rg {Number(LineHeight)} TL 6 {Number(height - 4 - FontSize)} Td\n");
        foreach (var line in text.Lines) content.Append(line).Append(" T*\n");
        content.Append("ET");

        var stream = content.ToString();
        return string.Create(CultureInfo.InvariantCulture,
            $"<< /Type /XObject /Subtype /Form /BBox [0 0 {Number(width)} {Number(height)}] /Resources << /Font {text.Resources} >> /Length {stream.Length} >>\nstream\n{stream}\nendstream");
    }

    private string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
