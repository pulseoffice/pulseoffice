using System.Globalization;
using System.Text;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public interface IPdfFormWriter
{
    IReadOnlyList<int> Widgets(PdfFile file, int pageId, IReadOnlyList<PrintMarkedField> fields, double pageHeight, ISet<string> names, IPdfAnnotationTagging? tagging = null);

    string? AcroForm(IReadOnlyList<int> fieldIds);
}

public sealed class PdfFormWriter : IPdfFormWriter
{
    private const int ReadOnlyFlag = 1;
    private const int MultiLineFlag = 1 << 12;
    private const int PushButtonFlag = 1 << 16;
    private const int ComboFlag = 1 << 17;
    private const int EditFlag = 1 << 18;
    private const string DefaultAppearance = "/Helv 10 Tf 0 g";

    public IReadOnlyList<int> Widgets(PdfFile file, int pageId, IReadOnlyList<PrintMarkedField> fields, double pageHeight, ISet<string> names, IPdfAnnotationTagging? tagging = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(names);
        var ids = new List<int>();
        foreach (var (area, field) in fields)
        {
            var left = area.Left.ToPoints();
            var top = pageHeight - area.Top.ToPoints();
            var width = Math.Max(1, area.Width.ToPoints());
            var height = Math.Max(1, area.Height.ToPoints());
            var rect = FormattableString.Invariant($"[{Number(left)} {Number(top - height)} {Number(left + width)} {Number(top)}]");
            var id = file.Reserve();
            var name = Unique(field.Name, names);
            var common = new StringBuilder();
            common.Append(CultureInfo.InvariantCulture,
                $"<< /Type /Annot /Subtype /Widget /Rect {rect} /F 4 /P {pageId} 0 R /T {PdfFile.TextString(name)}");
            common.Append(" /TU ").Append(PdfFile.TextString(field.Tooltip is { Length: > 0 } tooltip ? tooltip : name));
            if (field.Kind != PrintFormFieldKind.Picture) common.Append(" /MK << /BG [1 1 1] >>");
            if (tagging is not null) common.Append(tagging.Field(id, field));
            var flags = field.ReadOnly ? ReadOnlyFlag : 0;
            switch (field.Kind)
            {
                case PrintFormFieldKind.CheckBox:
                    var on = Appearance(file, width, height, true);
                    var off = Appearance(file, width, height, false);
                    var state = field.Checked ? "/Yes" : "/Off";
                    common.Append(CultureInfo.InvariantCulture,
                        $" /FT /Btn /Ff {flags} /V {state} /AS {state} /AP << /N << /Yes {on} 0 R /Off {off} 0 R >> >>");
                    break;
                case PrintFormFieldKind.Picture:
                    common.Append(CultureInfo.InvariantCulture,
                        $" /FT /Btn /Ff {flags | PushButtonFlag} /MK << /TP 1 >> /AP << /N {Blank(file, width, height)} 0 R >>");
                    break;
                case PrintFormFieldKind.ComboBox or PrintFormFieldKind.ListBox:
                    flags |= ComboFlag;
                    if (field.Kind == PrintFormFieldKind.ComboBox) flags |= EditFlag;
                    var options = field.Options.Count > 0 || field.Value.Length == 0 ? field.Options : [field.Value];
                    common.Append(CultureInfo.InvariantCulture, $" /FT /Ch /Ff {flags} /Opt [");
                    common.Append(string.Join(' ', options.Select(PdfFile.TextString)));
                    common.Append("] /V ").Append(PdfFile.TextString(field.Value)).Append(" /DA ").Append(PdfFile.TextString(DefaultAppearance));
                    common.Append(CultureInfo.InvariantCulture, $" /AP << /N {Blank(file, width, height)} 0 R >>");
                    break;
                default:
                    if (field.MultiLine) flags |= MultiLineFlag;
                    common.Append(CultureInfo.InvariantCulture, $" /FT /Tx /Ff {flags} /V ").Append(PdfFile.TextString(field.Value))
                        .Append(" /DA ").Append(PdfFile.TextString(DefaultAppearance));
                    common.Append(CultureInfo.InvariantCulture, $" /AP << /N {Blank(file, width, height)} 0 R >>");
                    break;
            }
            common.Append(" >>");
            file.WriteObject(id, common.ToString());
            ids.Add(id);
        }
        return ids;
    }

    public string? AcroForm(IReadOnlyList<int> fieldIds)
    {
        ArgumentNullException.ThrowIfNull(fieldIds);
        if (fieldIds.Count == 0) return null;
        return "<< /Fields [" + string.Join(' ', fieldIds.Select(id => id.ToString(CultureInfo.InvariantCulture) + " 0 R")) +
               "] /DA " + PdfFile.TextString(DefaultAppearance) +
               " /DR << /Font << /Helv << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >> >>";
    }

    private static string Unique(string name, ISet<string> names)
    {
        var stem = string.IsNullOrWhiteSpace(name) ? "field" : name.Trim().Replace('.', '_');
        var candidate = stem;
        for (var number = 2; !names.Add(candidate); number++) candidate = stem + "_" + number.ToString(CultureInfo.InvariantCulture);
        return candidate;
    }

    private static int Blank(PdfFile file, double width, double height) =>
        file.WriteStream([], false, FormattableString.Invariant($"/Type /XObject /Subtype /Form /BBox [0 0 {Number(width)} {Number(height)}]"));

    private static int Appearance(PdfFile file, double width, double height, bool checkedState)
    {
        var content = new StringBuilder(FormattableString.Invariant($"1 g 0 0 {Number(width)} {Number(height)} re f 0 G 0.75 w 0.5 0.5 {Number(width - 1)} {Number(height - 1)} re S"));
        if (checkedState)
        {
            var inset = Math.Min(width, height) * 0.2;
            content.Append(FormattableString.Invariant(
                $" 1 w {Number(inset)} {Number(inset)} m {Number(width - inset)} {Number(height - inset)} l S {Number(inset)} {Number(height - inset)} m {Number(width - inset)} {Number(inset)} l S"));
        }
        content.Append('\n');
        return file.WriteStream(Encoding.ASCII.GetBytes(content.ToString()), false,
            FormattableString.Invariant($"/Type /XObject /Subtype /Form /BBox [0 0 {Number(width)} {Number(height)}]"));
    }

    private static string Number(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
}
