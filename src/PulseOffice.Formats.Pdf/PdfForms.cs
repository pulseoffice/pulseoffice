using System.Globalization;
using System.Text;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public interface IPdfFormWriter
{
    PdfFormFields Begin();

    IReadOnlyList<int> Widgets(PdfFile file, int pageId, IReadOnlyList<PrintMarkedField> fields, double pageHeight, PdfFormFields form, IPdfAnnotationTagging? tagging = null);

    string? AcroForm(PdfFile file, PdfFormFields form);
}

public sealed class PdfFormFields
{
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PdfFieldGroup> _byIdentity = new(StringComparer.Ordinal);
    private readonly List<PdfFieldGroup> _groups = [];

    public IReadOnlyList<PdfFieldGroup> Groups => _groups;

    public PdfFieldGroup Group(PrintFormField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Identity is { } identity && _byIdentity.TryGetValue(identity, out var known))
        {
            known.Extend(field);
            return known;
        }
        var group = new PdfFieldGroup(Unique(field.Name), field);
        _groups.Add(group);
        if (field.Identity is { } key) _byIdentity[key] = group;
        return group;
    }

    private string Unique(string name)
    {
        var stem = string.IsNullOrWhiteSpace(name) ? "field" : name.Trim().Replace('.', '_');
        var candidate = stem;
        for (var number = 2; !_names.Add(candidate); number++) candidate = stem + "_" + number.ToString(CultureInfo.InvariantCulture);
        return candidate;
    }
}

public sealed class PdfFieldGroup(string name, PrintFormField field)
{
    private readonly List<(int Id, string Entries)> _widgets = [];

    public string Name { get; } = name;

    public PrintFormField Field { get; private set; } = field;

    public IReadOnlyList<(int Id, string Entries)> Widgets => _widgets;

    public void Extend(PrintFormField next)
    {
        ArgumentNullException.ThrowIfNull(next);
        Field = Field with { Value = Field.Value + next.Value, Checked = Field.Checked || next.Checked };
    }

    public void Add(int id, string entries) => _widgets.Add((id, entries));
}

public sealed class PdfFormWriter : IPdfFormWriter
{
    private const int ReadOnlyFlag = 1;
    private const int MultiLineFlag = 1 << 12;
    private const int PushButtonFlag = 1 << 16;
    private const int ComboFlag = 1 << 17;
    private const int EditFlag = 1 << 18;
    private const double FontSize = 10;
    private const double Padding = 2;
    private const string DefaultAppearance = "/Helv 10 Tf 0 g";
    private const string FontResource = "<< /Font << /Helv << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >>";

    public PdfFormFields Begin() => new();

    public IReadOnlyList<int> Widgets(PdfFile file, int pageId, IReadOnlyList<PrintMarkedField> fields, double pageHeight, PdfFormFields form, IPdfAnnotationTagging? tagging = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(form);
        var ids = new List<int>();
        foreach (var marked in fields)
        {
            var (area, field) = marked;
            var left = area.Left.ToPoints();
            var top = pageHeight - area.Top.ToPoints();
            var width = Math.Max(1, area.Width.ToPoints());
            var height = Math.Max(1, area.Height.ToPoints());
            var rect = FormattableString.Invariant($"[{Number(left)} {Number(top - height)} {Number(left + width)} {Number(top)}]");
            var id = file.Reserve();
            var entries = new StringBuilder();
            entries.Append(CultureInfo.InvariantCulture, $"/Type /Annot /Subtype /Widget /Rect {rect} /F 4 /P {pageId} 0 R");
            if (field.Kind != PrintFormFieldKind.Picture) entries.Append(" /MK << /BG [1 1 1] >>");
            if (tagging is not null) entries.Append(tagging.Field(id, marked));
            entries.Append(field.Kind switch
            {
                PrintFormFieldKind.CheckBox => FormattableString.Invariant(
                    $" /AS {(field.Checked ? "/Yes" : "/Off")} /AP << /N << /Yes {CheckAppearance(file, width, height, true)} 0 R /Off {CheckAppearance(file, width, height, false)} 0 R >> >>"),
                PrintFormFieldKind.Picture => FormattableString.Invariant($" /MK << /TP 1 >> /AP << /N {Blank(file, width, height)} 0 R >>"),
                _ => FormattableString.Invariant($" /AP << /N {TextAppearance(file, width, height, field)} 0 R >>"),
            });
            form.Group(field).Add(id, entries.ToString());
            ids.Add(id);
        }
        return ids;
    }

    public string? AcroForm(PdfFile file, PdfFormFields form)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(form);
        var roots = new List<int>();
        foreach (var group in form.Groups)
        {
            var fieldEntries = FieldEntries(group);
            if (group.Widgets.Count == 1)
            {
                var (id, entries) = group.Widgets[0];
                file.WriteObject(id, "<< " + entries + fieldEntries + " >>");
                roots.Add(id);
                continue;
            }
            var parent = file.Reserve();
            foreach (var (id, entries) in group.Widgets)
                file.WriteObject(id, "<< " + entries + FormattableString.Invariant($" /Parent {parent} 0 R >>"));
            file.WriteObject(parent, "<<" + fieldEntries + " /Kids [" + string.Join(' ', group.Widgets.Select(widget => widget.Id.ToString(CultureInfo.InvariantCulture) + " 0 R")) + "] >>");
            roots.Add(parent);
        }
        if (roots.Count == 0) return null;
        return "<< /Fields [" + string.Join(' ', roots.Select(id => id.ToString(CultureInfo.InvariantCulture) + " 0 R")) +
               "] /DA " + PdfFile.TextString(DefaultAppearance) + " /DR " + FontResource + " >>";
    }

    private static string FieldEntries(PdfFieldGroup group)
    {
        var field = group.Field;
        var result = new StringBuilder(" /T ").Append(PdfFile.TextString(group.Name));
        result.Append(" /TU ").Append(PdfFile.TextString(field.Tooltip is { Length: > 0 } tooltip ? tooltip : group.Name));
        var flags = field.ReadOnly ? ReadOnlyFlag : 0;
        switch (field.Kind)
        {
            case PrintFormFieldKind.CheckBox:
                result.Append(CultureInfo.InvariantCulture, $" /FT /Btn /Ff {flags} /V {(field.Checked ? "/Yes" : "/Off")}");
                break;
            case PrintFormFieldKind.Picture:
                result.Append(CultureInfo.InvariantCulture, $" /FT /Btn /Ff {flags | PushButtonFlag}");
                break;
            case PrintFormFieldKind.ComboBox or PrintFormFieldKind.ListBox:
                flags |= ComboFlag;
                if (field.Kind == PrintFormFieldKind.ComboBox) flags |= EditFlag;
                var options = field.Options.Count > 0 || field.Value.Length == 0 ? field.Options : [field.Value];
                result.Append(CultureInfo.InvariantCulture, $" /FT /Ch /Ff {flags} /Opt [");
                result.Append(string.Join(' ', options.Select(PdfFile.TextString)));
                result.Append("] /V ").Append(PdfFile.TextString(field.Value)).Append(" /DA ").Append(PdfFile.TextString(DefaultAppearance));
                break;
            default:
                if (field.MultiLine) flags |= MultiLineFlag;
                result.Append(CultureInfo.InvariantCulture, $" /FT /Tx /Ff {flags} /V ").Append(PdfFile.TextString(field.Value))
                    .Append(" /DA ").Append(PdfFile.TextString(DefaultAppearance));
                break;
        }
        return result.ToString();
    }

    private static int TextAppearance(PdfFile file, double width, double height, PrintFormField field)
    {
        var content = new StringBuilder(FormattableString.Invariant($"1 g 0 0 {Number(width)} {Number(height)} re f /Tx BMC"));
        var lines = field.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (field.Value.Length > 0)
        {
            var leading = FontSize * 1.15;
            var first = lines.Length == 1 && !field.MultiLine
                ? (height - FontSize * 0.7) / 2
                : height - Padding - FontSize * 0.8;
            content.Append(FormattableString.Invariant(
                $" q {Number(Padding / 2)} {Number(Padding / 2)} {Number(Math.Max(0, width - Padding))} {Number(Math.Max(0, height - Padding))} re W n BT /Helv {Number(FontSize)} Tf 0 g {Number(Padding)} {Number(first)} Td {Number(leading)} TL"));
            for (var index = 0; index < lines.Length; index++)
                content.Append(index == 0 ? " " : " T* ").Append(PdfEncoding.Literal(lines[index])).Append(" Tj");
            content.Append(" ET Q");
        }
        content.Append(" EMC\n");
        return file.WriteStream(Encoding.Latin1.GetBytes(content.ToString()), false,
            FormattableString.Invariant($"/Type /XObject /Subtype /Form /BBox [0 0 {Number(width)} {Number(height)}] /Resources {FontResource}"));
    }

    private static int Blank(PdfFile file, double width, double height) =>
        file.WriteStream([], false, FormattableString.Invariant($"/Type /XObject /Subtype /Form /BBox [0 0 {Number(width)} {Number(height)}]"));

    private static int CheckAppearance(PdfFile file, double width, double height, bool checkedState)
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
