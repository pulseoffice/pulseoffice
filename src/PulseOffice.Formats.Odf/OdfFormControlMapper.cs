using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfFormControlMapper
{
    private readonly List<XElement> _controls = [];
    private readonly FormControlCodec _codec = new();
    private readonly AnchoredFrames _frames = new(Namespaces.Draw + "control");

    public IReadOnlyList<XElement> Controls => _controls;

    public XElement BuildControlElement(string id, FormControlObject control) => _codec.Build(id, control);

    public FormControlObject? ReadControl(XElement declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (_codec.KindOf(declaration) is not { } kind) return null;

        return new FormControlObject
        {
            Id = declaration.Attribute(Namespaces.Form + "id")?.Value ?? Guid.NewGuid().ToString("N"),
            ControlKind = kind,
            Properties = _codec.Read(declaration),
        };
    }

    public XElement? BuildForms(Worksheet sheet, string formName, FormDocumentSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        _controls.Clear();

        var controls = sheet.Objects
            .Where(placed => placed.Content is FormControlObject)
            .ToList();

        if (controls.Count == 0) return null;

        var form = new XElement(
            Namespaces.Form + "form",
            new XAttribute(Namespaces.Form + "name", formName),
            new XAttribute(Namespaces.Form + "apply-filter", "true"),
            new XAttribute(Namespaces.Form + "command-type", "table"));

        foreach (var placed in controls)
        {
            if (placed.Content is not FormControlObject control) continue;

            form.Add(BuildControl(placed.Id, control, sheet.Name));
        }

        return Forms(settings ?? new FormDocumentSettings(), form);
    }

    public XElement Forms(FormDocumentSettings settings, XElement form)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(form);

        if (settings.DataSource is { Length: > 0 } source) form.SetAttributeValue(Namespaces.Form + "datasource", source);
        if (settings.Command is { Length: > 0 } command)
        {
            form.SetAttributeValue(Namespaces.Form + "command", command);
            form.SetAttributeValue(Namespaces.Form + "command-type", "command");
        }

        return new(Namespaces.Office + "forms",
            new XAttribute(Namespaces.Form + "automatic-focus", settings.AutomaticFocus ? "true" : "false"),
            new XAttribute(Namespaces.Form + "apply-design-mode", settings.OpenInDesignMode ? "true" : "false"),
            form);
    }

    public XElement? SettingsOnly(FormDocumentSettings settings, string formName)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings == new FormDocumentSettings()
            ? null
            : Forms(settings, new XElement(Namespaces.Form + "form", new XAttribute(Namespaces.Form + "name", formName)));
    }

    public FormDocumentSettings? ReadSettings(XElement? forms)
    {
        if (forms is null) return null;
        var form = forms.Element(Namespaces.Form + "form");
        return new FormDocumentSettings(
            OpenInDesignMode: forms.Attribute(Namespaces.Form + "apply-design-mode")?.Value == "true",
            AutomaticFocus: forms.Attribute(Namespaces.Form + "automatic-focus")?.Value == "true",
            DataSource: form?.Attribute(Namespaces.Form + "datasource")?.Value,
            Command: form?.Attribute(Namespaces.Form + "command")?.Value);
    }

    public IReadOnlyDictionary<CellAddress, List<XElement>> BuildFrames(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var frames = new Dictionary<CellAddress, List<XElement>>();

        foreach (var placed in sheet.Objects)
        {
            if (placed.Content is not FormControlObject) continue;

            var frame = new XElement(
                Namespaces.Draw + "control",
                new XAttribute(Namespaces.Draw + "control", System.Xml.XmlConvert.EncodeLocalName(placed.Id)),
                new XAttribute(Namespaces.Text + "anchor-type", "frame"),
                new XAttribute(Namespaces.Svg + "x", Length(placed.OffsetX)),
                new XAttribute(Namespaces.Svg + "y", Length(placed.OffsetY)),
                new XAttribute(Namespaces.Svg + "width", Length(placed.Size.Width)),
                new XAttribute(Namespaces.Svg + "height", Length(placed.Size.Height)),
                new XAttribute(Namespaces.Draw + "z-index",
                    placed.ZOrder.ToString(CultureInfo.InvariantCulture)));

            if (!frames.TryGetValue(placed.Anchor, out var list))
                frames[placed.Anchor] = list = [];

            list.Add(frame);
        }

        return frames;
    }

    public IReadOnlyList<PlacedObject> ReadControls(XElement table, XElement? forms)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (forms is null) return [];

        var declared = forms.Descendants()
            .Where(element => element.Name.Namespace == Namespaces.Form && _codec.KindOf(element) is not null)
            .GroupBy(element => element.Attribute(Namespaces.Xml + "id")?.Value ?? element.Attribute(Namespaces.Form + "id")?.Value ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First());

        var placed = new List<PlacedObject>();
        foreach (var (frame, anchor) in _frames.In(table))
        {
            var reference = frame.Attribute(Namespaces.Draw + "control")?.Value;
            if (reference is null || !declared.TryGetValue(reference, out var element)) continue;
            var id = element.Attribute(Namespaces.Form + "id")?.Value ?? reference;
            if (_codec.KindOf(element) is not { } kind) continue;

            placed.Add(new PlacedObject
            {
                Id = id,
                Anchor = anchor,
                OffsetX = Parse(frame.Attribute(Namespaces.Svg + "x")?.Value),
                OffsetY = Parse(frame.Attribute(Namespaces.Svg + "y")?.Value),
                Size = new DocumentSize(
                    Parse(frame.Attribute(Namespaces.Svg + "width")?.Value),
                    Parse(frame.Attribute(Namespaces.Svg + "height")?.Value)),
                ZOrder = int.TryParse(frame.Attribute(Namespaces.Draw + "z-index")?.Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out var order) ? order : 0,
                Content = new FormControlObject { Id = id, ControlKind = kind, Properties = _codec.Read(element, (string?)table.Attribute(Namespaces.Table + "name")) },
            });
        }
        return placed;
    }

    private XElement BuildControl(string id, FormControlObject control, string sheetName)
    {
        var element = _codec.Build(id, control, sheetName);
        _controls.Add(element);
        return element;
    }

    private string Length(Twip value) =>
        value.ToCentimeters().ToString("0.###", CultureInfo.InvariantCulture) + "cm";

    private Twip Parse(string? text)
    {
        return new OdfLength().Parse(text, Twip.Zero);
    }
}
