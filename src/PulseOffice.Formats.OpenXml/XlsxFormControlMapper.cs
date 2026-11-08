using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using X14 = DocumentFormat.OpenXml.Office2010.Excel;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed record DrawnControl(PlacedObject Placed, FormControlObject Form, int ShapeNumber);

public sealed class XlsxFormControlMapper(ISheetPlacement placement)
{
    private readonly ControlNameCodec _names = new();
    private readonly X14ControlPropertiesCodec _properties = new();
    private readonly VmlControlCodec _vml = new();
    private readonly XlsxVmlAnchor _vmlAnchors = new(new SheetPlacement());
    private readonly XlsxCellAnchors _cellAnchors = new();

    public XlsxFormControlMapper() : this(new SheetPlacement()) { }
    private readonly VmlStyle _styles = new();
    private readonly XlsxVmlDocuments _documents = new();

    public IReadOnlyList<DrawnControl> Assign(Worksheet sheet, XlsxVmlShapes shapes)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(shapes);

        return sheet.Objects
            .Where(item => item.Content is FormControlObject)
            .OrderBy(item => item.ZOrder)
            .Select(item => new DrawnControl(item, (FormControlObject)item.Content, shapes.NextNumber()))
            .ToList();
    }

    public IEnumerable<XElement> Shapes(Worksheet sheet, IReadOnlyList<DrawnControl> drawn, XlsxVmlShapes shapes)
    {
        if (drawn.Count == 0) yield break;
        yield return _vml.ShapeType(shapes);

        foreach (var control in drawn)
        {
            var item = control.Placed;
            var left = sheet.Layout.ColumnOffset(item.Anchor.Column) + item.OffsetX;
            var top = sheet.Layout.RowOffset(item.Anchor.Row) + item.OffsetY;
            var style = string.Create(CultureInfo.InvariantCulture,
                $"position:absolute;margin-left:{_styles.Points(left)};margin-top:{_styles.Points(top)};width:{_styles.Points(item.Size.Width)};height:{_styles.Points(item.Size.Height)};z-index:{Math.Max(0, item.ZOrder) + 1}");
            yield return _vml.Shape(control.Form, shapes, shapes.Name(control.ShapeNumber), style, _vmlAnchors.Write(sheet.Layout, item));
        }
    }

    public void Write(WorksheetPart part, Worksheet sheet, IReadOnlyList<DrawnControl> drawn)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(sheet);
        if (drawn.Count == 0) return;

        var controls = new Controls();
        foreach (var control in drawn)
        {
            var propertiesPart = part.AddNewPart<ControlPropertiesPart>();
            propertiesPart.FormControlProperties = _properties.Build(control.Form);
            controls.AppendChild(Choice(Standard(part.GetIdOfPart(propertiesPart), sheet, control)));
        }

        part.Worksheet?.AppendChild(Choice(controls));
    }

    private Control Standard(string relationship, Worksheet sheet, DrawnControl control)
    {
        var item = control.Placed;
        var end = placement.End(sheet.Layout, item);
        var properties = control.Form.Properties;
        return new Control(
            new ControlProperties(
                new ObjectAnchor(
                    _cellAnchors.Marker(new FromMarker(), item.Anchor, item.OffsetX, item.OffsetY),
                    _cellAnchors.Marker(new ToMarker(), end.Cell, end.X, end.Y))
                {
                    MoveWithCells = item.Anchoring != ObjectAnchoring.Fixed,
                    SizeWithCells = item.Anchoring == ObjectAnchoring.MoveAndSize ? true : null,
                })
            {
                DefaultSize = false,
                AutoFill = false,
                AutoLine = false,
                AutoPict = false,
                Print = properties.Printable,
                Disabled = properties.Enabled ? null : true,
                AltText = properties.Tooltip,
            })
        {
            ShapeId = (uint)control.ShapeNumber,
            Id = relationship,
            Name = properties.Name.Length > 0 ? properties.Name : item.Id,
        };
    }

    private AlternateContent Choice(OpenXmlElement content)
    {
        var alternate = new AlternateContent();
        alternate.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");
        alternate.AppendChild(new AlternateContentChoice(content) { Requires = "x14" });
        return alternate;
    }

    public void Read(WorksheetPart part, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(sheet);

        if (part.Worksheet is not { } content) return;

        var anchors = new XlsxAnchorLayout(sheet.Layout);
        var drawn = Drawn(part);
        var vmlShapes = VmlShapes(part);
        var used = new HashSet<string>(sheet.Objects.Select(item => item.Id), StringComparer.Ordinal);
        var ordinal = 0;
        var claimed = new HashSet<int>();

        foreach (var control in content.Descendants<Control>().ToList())
        {
            var propertiesPart = control.Id?.Value is { } id ? Part(part, id) : null;
            var element = propertiesPart?.FormControlProperties;
            var number = control.ShapeId?.Value is { } shapeId ? (int)shapeId : -1;
            var shape = vmlShapes.GetValueOrDefault(number);
            if (shape is not null) claimed.Add(number);

            var placement = _names.Read(control.Name?.Value) ?? Foreign(sheet, control, element, shape, drawn, anchors, ++ordinal);
            if (placement is null) continue;
            placement = placement with { Anchoring = AnchoringOf(control) };

            var properties = element is null && shape?.Element(_vml.Excel + "ClientData") is { } data
                ? _vml.Read(data, placement.Kind, placement.Properties)
                : _properties.Read(element, placement.Kind, placement.Properties);

            Add(sheet, used, placement, properties);
        }

        foreach (var (number, shape) in vmlShapes)
        {
            if (claimed.Contains(number)) continue;
            if (Legacy(sheet, shape, ++ordinal) is not { } placement) continue;
            var properties = _vml.Read(shape.Element(_vml.Excel + "ClientData")!, placement.Kind, placement.Properties);
            Add(sheet, used, placement, properties);
        }
    }

    private ObjectAnchoring AnchoringOf(Control control) =>
        control.ControlProperties?.ObjectAnchor is not { } anchor ? ObjectAnchoring.Move
        : anchor.SizeWithCells?.Value == true ? ObjectAnchoring.MoveAndSize
        : anchor.MoveWithCells?.Value == true ? ObjectAnchoring.Move
        : ObjectAnchoring.Fixed;

    private void Add(Worksheet sheet, HashSet<string> used, DescribedControl placement, FormControlProperties properties)
    {
        var id = placement.Id;
        for (var suffix = 2; !used.Add(id); suffix++) id = placement.Id + "_" + suffix.ToString(CultureInfo.InvariantCulture);

        properties = properties with
        {
            LinkedCell = Owned(properties.LinkedCell, sheet.Name),
            SourceRange = Owned(properties.SourceRange, sheet.Name),
        };
        sheet.Objects.Add(new PlacedObject
        {
            Id = id,
            Anchor = placement.Anchor,
            OffsetX = placement.OffsetX,
            OffsetY = placement.OffsetY,
            Size = placement.Size,
            Anchoring = placement.Anchoring,
            Content = new FormControlObject { Id = id, ControlKind = placement.Kind, Properties = properties },
        });
    }

    private readonly SheetReferenceText _references = new();

    private string? Owned(string? reference, string ownSheet) =>
        reference is null ? null : _references.Split(reference) is var split ? _references.Qualify(split.Sheet, ownSheet, split.Local) : null;

    private DescribedControl? Foreign(Worksheet sheet, Control control, X14.FormControlProperties? element, XElement? shape,
        IReadOnlyDictionary<uint, (string Name, string Text)> drawn, XlsxAnchorLayout anchors, int ordinal)
    {
        var kind = element?.ObjectType?.Value is { } type ? _properties.KindOf(type) : null;
        kind ??= shape is null ? null : _vml.KindOf(shape);
        if (kind is not { } found) return null;

        var seed = new PlacedObject { Id = string.Empty, Anchor = new CellAddress(0, 0), Content = new FormControlObject { Id = string.Empty, ControlKind = found, Properties = new() } };
        PlacedObject placed;
        if (control.Descendants().FirstOrDefault(child => child.LocalName == "anchor") is { } anchor)
        {
            var box = anchors.Markers(
                anchor.ChildElements.FirstOrDefault(child => child.LocalName == "from"),
                anchor.ChildElements.FirstOrDefault(child => child.LocalName == "to"));
            placed = new SheetPlacement().Place(anchors.Layout, new CellAddress(0, 0), seed, box);
        }
        else if (shape is not null && _vmlAnchors.Read(sheet.Layout, _vml.Anchor(shape)) is { } box)
        {
            placed = _vmlAnchors.Place(sheet.Layout, seed, box);
        }
        else
        {
            return null;
        }

        var drawing = control.ShapeId?.Value is { } shapeId && drawn.TryGetValue(shapeId, out var match) ? match : default;
        var name = control.Name?.Value is { Length: > 0 } given ? given : drawing.Name is { Length: > 0 } ? drawing.Name : $"control{ordinal}";
        var text = shape is null ? string.Empty : _vml.Text(shape);
        var label = text.Length > 0 ? text : drawing.Text is { Length: > 0 } ? drawing.Text : null;

        var settings = control.ControlProperties;
        return new DescribedControl(name, found, placed.Anchor, placed.Size,
            new FormControlProperties { Name = name, Label = label, Tooltip = settings?.AltText?.Value, Printable = settings?.Print?.Value ?? true },
            placed.OffsetX, placed.OffsetY);
    }

    private DescribedControl? Legacy(Worksheet sheet, XElement shape, int ordinal)
    {
        if (_vml.KindOf(shape) is not { } kind) return null;
        if (_vmlAnchors.Read(sheet.Layout, _vml.Anchor(shape)) is not { } box) return null;

        var seed = new PlacedObject { Id = string.Empty, Anchor = new CellAddress(0, 0), Content = new FormControlObject { Id = string.Empty, ControlKind = kind, Properties = new() } };
        var placed = _vmlAnchors.Place(sheet.Layout, seed, box);
        var name = $"control{ordinal}";
        var text = _vml.Text(shape);
        return new DescribedControl(name, kind, placed.Anchor, placed.Size,
            new FormControlProperties { Name = name, Label = text.Length > 0 ? text : null }, placed.OffsetX, placed.OffsetY);
    }

    private Dictionary<int, XElement> VmlShapes(WorksheetPart part)
    {
        var shapes = new Dictionary<int, XElement>();
        foreach (var document in _documents.Load(part))
            foreach (var shape in document.Descendants(_vml.Vml + "shape"))
            {
                if (_vml.KindOf(shape) is null) continue;
                if (_vml.Number(shape) is { } number) shapes.TryAdd(number, shape);
            }
        return shapes;
    }

    private IReadOnlyDictionary<uint, (string Name, string Text)> Drawn(WorksheetPart part)
    {
        var shapes = new Dictionary<uint, (string Name, string Text)>();
        if (part.DrawingsPart?.WorksheetDrawing is not { } drawing) return shapes;

        foreach (var properties in drawing.Descendants<DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualDrawingProperties>())
        {
            if (properties.Id?.Value is not { } id) continue;
            var shape = properties.Parent?.Parent;
            var text = string.Concat(shape?.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(run => run.Text) ?? []);
            shapes[id] = (properties.Name?.Value ?? string.Empty, text);
        }
        return shapes;
    }

    private ControlPropertiesPart? Part(WorksheetPart part, string id)
    {
        try { return part.GetPartById(id) as ControlPropertiesPart; }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}

internal sealed record DescribedControl(string Id, FormControlKind Kind, CellAddress Anchor, DocumentSize Size, FormControlProperties Properties, Twip OffsetX = default, Twip OffsetY = default)
{
    public ObjectAnchoring Anchoring { get; init; } = ObjectAnchoring.Move;
}

internal sealed class ControlNameCodec
{
    private const string Prefix = "pulsecontrol:";

    public DescribedControl? Read(string? name)
    {
        if (name is null || !name.StartsWith(Prefix, StringComparison.Ordinal)) return null;

        var parts = name[Prefix.Length..].Split('|');
        if (parts.Length < 8) return null;
        if (!Enum.TryParse<FormControlKind>(parts[0], out var kind)) return null;
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)) return null;
        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var column)) return null;
        if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)) return null;
        if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var height)) return null;

        var label = Unescape(parts[7]);
        return new DescribedControl(
            Unescape(parts[1]), kind, new CellAddress(row, column),
            new DocumentSize(new Twip((int)Math.Round(width)), new Twip((int)Math.Round(height))),
            new FormControlProperties { Name = Unescape(parts[6]), Label = label.Length == 0 ? null : label },
            Offset(parts, 8), Offset(parts, 9));
    }

    private Twip Offset(string[] parts, int index) =>
        parts.Length > index && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? new Twip(value) : Twip.Zero;

    private string Unescape(string text) => text.Replace("%7C", "|", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal);
}

internal sealed class FormControlValueIndex
{
    public bool Indexed(FormControlKind kind) => kind is FormControlKind.ListBox or FormControlKind.ComboBox;

    public bool Oriented(FormControlKind kind) => kind is FormControlKind.ScrollBar or FormControlKind.SpinButton;

    public int? Write(FormControlKind kind, FormControlValue value) =>
        Indexed(kind)
            ? value.SelectedIndices.Count > 0 ? value.SelectedIndices[0] + 1 : null
            : value.Number is { } number ? (int)Math.Max(0, Math.Round(number)) : null;

    public FormControlValue Read(FormControlKind kind, FormControlValue value, int current) =>
        Indexed(kind)
            ? current > 0 ? value with { SelectedIndices = [current - 1] } : value
            : value with { Number = current };

    public string? Reference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var bang = reference.LastIndexOf('!');
        var local = reference.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        return local.Length == 0 || bang == reference.Length - 1 ? null : local;
    }
}

internal sealed class X14ControlPropertiesCodec
{
    private readonly FormControlValueIndex _values = new();

    private readonly IReadOnlyDictionary<FormControlKind, X14.ObjectTypeValues> _types = new Dictionary<FormControlKind, X14.ObjectTypeValues>
    {
        [FormControlKind.CheckBox] = X14.ObjectTypeValues.CheckBox,
        [FormControlKind.OptionButton] = X14.ObjectTypeValues.Radio,
        [FormControlKind.ListBox] = X14.ObjectTypeValues.List,
        [FormControlKind.ComboBox] = X14.ObjectTypeValues.Drop,
        [FormControlKind.PushButton] = X14.ObjectTypeValues.Button,
        [FormControlKind.ImageButton] = X14.ObjectTypeValues.Button,
        [FormControlKind.Label] = X14.ObjectTypeValues.Label,
        [FormControlKind.GroupBox] = X14.ObjectTypeValues.GroupBox,
        [FormControlKind.ScrollBar] = X14.ObjectTypeValues.Scroll,
        [FormControlKind.SpinButton] = X14.ObjectTypeValues.Spin,
    };

    public X14.FormControlProperties Build(FormControlObject form)
    {
        var source = form.Properties;
        var kind = form.ControlKind;
        var properties = new X14.FormControlProperties
        {
            ObjectType = _types.TryGetValue(kind, out var type) ? type : X14.ObjectTypeValues.EditBox,
            LockText = !source.Enabled,
        };

        if (source.LinkedCell is { } cell) properties.FmlaLink = cell;
        if (source.SourceRange is { } range) properties.FmlaRange = range;
        if (source.Group is { } group) properties.FmlaGroup = group;

        if (source.Range is { } numeric)
        {
            properties.Min = Whole(numeric.Minimum, 0);
            properties.Max = Whole(numeric.Maximum, 0);
            properties.Incremental = Whole(numeric.Step, 1);
            properties.Page = Whole(numeric.Page, 1);
        }

        if (_values.Write(kind, source.Value) is { } current) properties.Val = (uint)current;
        if (source.Value.Checked == CheckState.Checked) properties.Checked = X14.CheckedValues.Checked;
        if (source.Value.Checked == CheckState.Indeterminate) properties.Checked = X14.CheckedValues.Mixed;

        if (kind == FormControlKind.ComboBox)
        {
            properties.DropLines = (uint)Math.Max(1, source.DropdownLines);
            properties.DropStyle = X14.DropStyleValues.Combo;
        }

        if (kind == FormControlKind.ListBox)
            properties.SelectionType = source.MultiSelect ? X14.SelectionTypeValues.Multiple : X14.SelectionTypeValues.Single;

        if (source.MultiLine) properties.MultipleLines = true;
        if (_values.Oriented(kind) && source.Orientation == FormOrientation.Horizontal) properties.Horizontal = true;
        return properties;
    }

    public FormControlKind? KindOf(X14.ObjectTypeValues type) =>
        _types.Where(pair => pair.Value == type).Select(pair => (FormControlKind?)pair.Key).FirstOrDefault()
        ?? (type == X14.ObjectTypeValues.EditBox ? FormControlKind.TextBox : null);

    public FormControlProperties Read(X14.FormControlProperties? element, FormControlKind kind, FormControlProperties fallback)
    {
        if (element is null) return fallback;

        var properties = fallback with
        {
            Enabled = element.LockText?.Value != true,
            LinkedCell = _values.Reference(element.FmlaLink?.Value) ?? fallback.LinkedCell,
            SourceRange = _values.Reference(element.FmlaRange?.Value) ?? fallback.SourceRange,
            Group = element.FmlaGroup?.Value ?? fallback.Group,
            MultiLine = element.MultipleLines?.Value == true,
            MultiSelect = element.SelectionType?.Value is { } selection && selection != X14.SelectionTypeValues.Single,
            DropdownLines = element.DropLines?.Value is { } lines ? (int)lines : fallback.DropdownLines,
            Orientation = _values.Oriented(kind)
                ? element.Horizontal?.Value == true ? FormOrientation.Horizontal : FormOrientation.Vertical
                : fallback.Orientation,
        };

        if (element.Min?.Value is { } min && element.Max?.Value is { } max)
            properties = properties with { Range = new NumericRange(min, max, element.Incremental?.Value ?? 1, element.Page?.Value ?? 10) };

        var value = properties.Value;
        if (element.Val?.Value is { } current) value = _values.Read(kind, value, (int)current);
        if (element.Checked?.Value is { } state)
            value = value with { Checked = state == X14.CheckedValues.Checked ? CheckState.Checked : state == X14.CheckedValues.Mixed ? CheckState.Indeterminate : CheckState.Unchecked };

        return properties with { Value = value };
    }

    private uint Whole(double value, double minimum) => (uint)Math.Max(minimum, Math.Round(value));
}

internal sealed class VmlControlCodec
{
    private const string ShapeTypeId = "_x0000_t201";
    private readonly FormControlValueIndex _values = new();

    private readonly IReadOnlyDictionary<FormControlKind, string> _types = new Dictionary<FormControlKind, string>
    {
        [FormControlKind.CheckBox] = "Checkbox",
        [FormControlKind.OptionButton] = "Radio",
        [FormControlKind.ListBox] = "List",
        [FormControlKind.ComboBox] = "Drop",
        [FormControlKind.PushButton] = "Button",
        [FormControlKind.ImageButton] = "Button",
        [FormControlKind.Label] = "Label",
        [FormControlKind.GroupBox] = "GBox",
        [FormControlKind.ScrollBar] = "Scroll",
        [FormControlKind.SpinButton] = "Spin",
        [FormControlKind.TextBox] = "Edit",
    };

    public XNamespace Vml { get; } = "urn:schemas-microsoft-com:vml";

    public XNamespace Office { get; } = "urn:schemas-microsoft-com:office:office";

    public XNamespace Excel { get; } = "urn:schemas-microsoft-com:office:excel";

    public XElement ShapeType(XlsxVmlShapes shapes) =>
        new(shapes.Vml + "shapetype",
            new XAttribute("id", ShapeTypeId), new XAttribute("coordsize", "21600,21600"), new XAttribute(shapes.Office + "spt", "201"), new XAttribute("path", "m,l,21600r21600,l21600,xe"),
            new XElement(shapes.Vml + "stroke", new XAttribute("joinstyle", "miter")),
            new XElement(shapes.Vml + "path", new XAttribute("shadowok", "f"), new XAttribute(shapes.Office + "extrusionok", "f"), new XAttribute("strokeok", "f"), new XAttribute("fillok", "f"), new XAttribute(shapes.Office + "connecttype", "rect")),
            new XElement(shapes.Office + "lock", new XAttribute(shapes.Vml + "ext", "edit"), new XAttribute("shapetype", "t")));

    public XElement Shape(FormControlObject form, XlsxVmlShapes shapes, string id, string style, string anchor)
    {
        var kind = form.ControlKind;
        var source = form.Properties;
        var button = _types.GetValueOrDefault(kind) == "Button";
        var shape = new XElement(shapes.Vml + "shape",
            new XAttribute("id", id),
            new XAttribute("type", "#" + ShapeTypeId),
            new XAttribute("style", style),
            button ? new XAttribute(shapes.Office + "button", "t") : null,
            new XAttribute("fillcolor", button ? "buttonFace [67]" : "window [65]"),
            new XAttribute("strokecolor", "windowText [64]"),
            new XAttribute(shapes.Office + "insetmode", "auto"),
            button ? null : new XAttribute("filled", "f"),
            button ? null : new XAttribute("stroked", "f"));

        if (Labelled(kind) && source.Label is { Length: > 0 } label)
            shape.Add(new XElement(shapes.Vml + "textbox", new XAttribute("style", "mso-direction-alt:auto"), new XAttribute(shapes.Office + "singleclick", "f"),
                new XElement("div", new XAttribute("style", button ? "text-align:center" : "text-align:left"),
                    new XElement("font", new XAttribute("face", source.Text.Family), new XAttribute("size", ((int)Math.Round(source.Text.Size * 20)).ToString(CultureInfo.InvariantCulture)), new XAttribute("color", "auto"), Lines(label)))));

        shape.Add(ClientData(form, shapes, anchor));
        return shape;
    }

    private IEnumerable<object> Lines(string label) =>
        label.Split('\n').SelectMany((line, index) => index == 0 ? new object[] { line } : [new XElement("br"), line]);

    private XElement ClientData(FormControlObject form, XlsxVmlShapes shapes, string anchor)
    {
        var kind = form.ControlKind;
        var source = form.Properties;
        var data = new XElement(shapes.Excel + "ClientData", new XAttribute("ObjectType", _types.GetValueOrDefault(kind, "Edit")));
        void Add(string name, object? content = null) => data.Add(new XElement(shapes.Excel + name, content));

        Add("Anchor", anchor);
        if (!source.Printable) Add("PrintObject", "False");
        Add("AutoFill", "False");
        if (Labelled(kind))
        {
            Add("AutoLine", "False");
            if (kind is FormControlKind.CheckBox or FormControlKind.OptionButton) Add("TextVAlign", "Center");
        }
        if (source.LinkedCell is { Length: > 0 } link) Add("FmlaLink", link);
        if (source.SourceRange is { Length: > 0 } range) Add("FmlaRange", range);
        if (source.Group is { Length: > 0 } group) Add("FmlaGroup", group);
        if (source.Value.Checked == CheckState.Checked) Add("Checked", "1");
        if (source.Value.Checked == CheckState.Indeterminate) Add("Checked", "2");
        if (_values.Write(kind, source.Value) is { } current) Add("Val", current.ToString(CultureInfo.InvariantCulture));
        if (source.Range is { } numeric)
        {
            Add("Min", Whole(numeric.Minimum));
            Add("Max", Whole(numeric.Maximum));
            Add("Inc", Whole(numeric.Step));
            Add("Page", Whole(numeric.Page));
        }
        if (_values.Oriented(kind) && source.Orientation == FormOrientation.Horizontal) Add("Horiz");
        if (kind == FormControlKind.ComboBox)
        {
            Add("DropStyle", "Combo");
            Add("DropLines", Math.Max(1, source.DropdownLines).ToString(CultureInfo.InvariantCulture));
        }
        if (kind == FormControlKind.ListBox) Add("SelType", source.MultiSelect ? "Multi" : "Single");
        if (kind == FormControlKind.TextBox && source.MultiLine) Add("MultiLine");
        if (kind is FormControlKind.ListBox or FormControlKind.ComboBox or FormControlKind.CheckBox or FormControlKind.OptionButton) Add("NoThreeD");
        return data;
    }

    public FormControlKind? KindOf(XElement shape)
    {
        var type = (string?)shape.Element(Excel + "ClientData")?.Attribute("ObjectType");
        if (type is null) return null;
        foreach (var (kind, name) in _types)
            if (string.Equals(name, type, StringComparison.OrdinalIgnoreCase)) return kind;
        return null;
    }

    public string? Anchor(XElement shape) => (string?)shape.Element(Excel + "ClientData")?.Element(Excel + "Anchor");

    public int? Number(XElement shape)
    {
        var id = (string?)shape.Attribute(Office + "spid") ?? (string?)shape.Attribute("id") ?? string.Empty;
        var digits = new string(id.Reverse().TakeWhile(char.IsAsciiDigit).Reverse().ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    public string Text(XElement shape)
    {
        if (shape.Element(Vml + "textbox") is not { } box) return string.Empty;
        var lines = box.Descendants().Where(element => element.Name.LocalName == "div").Select(Line);
        return string.Join("\n", lines.Where(line => line.Length > 0));
    }

    private string Line(XElement division)
    {
        var parts = division.DescendantNodes().Select(node => node switch
        {
            XText text => string.Join(' ', text.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) is var words && words.Length > 0
                ? (char.IsWhiteSpace(text.Value[0]) ? " " : string.Empty) + words + (char.IsWhiteSpace(text.Value[^1]) ? " " : string.Empty)
                : text.Value.Length > 0 ? " " : string.Empty,
            XElement { Name.LocalName: "br" } => "\n",
            _ => string.Empty,
        });
        var lines = string.Concat(parts).Split('\n').Select(line => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        return string.Join("\n", lines).Trim('\n');
    }

    public FormControlProperties Read(XElement data, FormControlKind kind, FormControlProperties fallback)
    {
        string? Value(string name) => (string?)data.Element(Excel + name);
        bool Has(string name) => data.Element(Excel + name) is { } element && !string.Equals(element.Value.Trim(), "False", StringComparison.OrdinalIgnoreCase);
        int? Number(string name) => int.TryParse(Value(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

        var properties = fallback with
        {
            Printable = Value("PrintObject") is not { } print || !string.Equals(print.Trim(), "False", StringComparison.OrdinalIgnoreCase),
            LinkedCell = _values.Reference(Value("FmlaLink")) ?? fallback.LinkedCell,
            SourceRange = _values.Reference(Value("FmlaRange")) ?? fallback.SourceRange,
            Group = Value("FmlaGroup") ?? fallback.Group,
            MultiLine = Has("MultiLine"),
            MultiSelect = Value("SelType") is { } selection && !string.Equals(selection, "Single", StringComparison.OrdinalIgnoreCase),
            DropdownLines = Number("DropLines") ?? fallback.DropdownLines,
            Orientation = _values.Oriented(kind) ? Has("Horiz") ? FormOrientation.Horizontal : FormOrientation.Vertical : fallback.Orientation,
        };

        if (Number("Min") is { } min && Number("Max") is { } max)
            properties = properties with { Range = new NumericRange(min, max, Number("Inc") ?? 1, Number("Page") ?? 10) };

        var value = properties.Value;
        if (Number("Val") is { } current) value = _values.Read(kind, value, current);
        if (Number("Checked") is { } state)
            value = value with { Checked = state == 1 ? CheckState.Checked : state == 2 ? CheckState.Indeterminate : CheckState.Unchecked };

        return properties with { Value = value };
    }

    private bool Labelled(FormControlKind kind) =>
        kind is FormControlKind.CheckBox or FormControlKind.OptionButton or FormControlKind.PushButton or FormControlKind.ImageButton
            or FormControlKind.Label or FormControlKind.GroupBox or FormControlKind.TextBox;

    private string Whole(double value) => ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
}

public sealed class XlsxFormControlFidelity
{
    private readonly FormControlProperties _defaults = new();
    private readonly FormTextStyle _text = new();

    private readonly IReadOnlySet<FormControlKind> _standard = new HashSet<FormControlKind>
    {
        FormControlKind.CheckBox, FormControlKind.OptionButton, FormControlKind.ListBox, FormControlKind.ComboBox,
        FormControlKind.PushButton, FormControlKind.Label, FormControlKind.GroupBox, FormControlKind.ScrollBar, FormControlKind.SpinButton,
    };

    public bool Lossy(FormControlObject form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var source = form.Properties;
        return !_standard.Contains(form.ControlKind)
            || (source.Items.Count > 0 && source.SourceRange is null)
            || source.Value.SelectedIndices.Count > 1
            || source.Value.Text is not null
            || source.Default != _defaults.Default
            || source.Text != _text
            || source.Visible != _defaults.Visible
            || source.ReadOnly || !source.TabStop || source.TabIndex != 0 || source.TriState
            || source.FormatCode is not null || source.ImagePath is not null
            || source.BackgroundHex is not null || source.BorderHex is not null || source.Border != _defaults.Border
            || source.MaxTextLength != 0 || source.PasswordCharacter is not null
            || source.ButtonAction != FormButtonAction.None || source.Toggle || source.TargetUrl is not null
            || source.DataField is not null || source.HelpText is not null || source.HelpUrl is not null;
    }
}
