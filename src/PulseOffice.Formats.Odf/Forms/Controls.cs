using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Document.Forms;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class FormListeners(OdfFormVocabulary vocabulary)
{
    private const string Prefix = "PulseOffice.";

    public XElement? Build(FormControlProperties properties) =>
        properties.Events.Count == 0
            ? null
            : new XElement(Namespaces.Office + "event-listeners",
                properties.Events.OrderBy(pair => pair.Key).Select(pair => new XElement(Namespaces.Script + "event-listener",
                    new XAttribute(Namespaces.Script + "language", "ooo:script"),
                    new XAttribute(Namespaces.Script + "event-name", vocabulary.Events.Token(pair.Key)),
                    new XAttribute(Namespaces.Xlink + "href", $"vnd.sun.star.script:{Prefix}{pair.Value}?language=PulseOffice&location=document"),
                    new XAttribute(Namespaces.Xlink + "type", "simple"))));

    public IReadOnlyDictionary<FormControlEvent, string> Read(XElement element)
    {
        var events = new Dictionary<FormControlEvent, string>();

        foreach (var listener in element.Elements(Namespaces.Office + "event-listeners").Elements(Namespaces.Script + "event-listener"))
        {
            if (vocabulary.Events.Find(listener.Attribute(Namespaces.Script + "event-name")?.Value) is not { } raised) continue;
            if (listener.Attribute(Namespaces.Xlink + "href")?.Value is not { } href) continue;

            var start = href.IndexOf(Prefix, StringComparison.Ordinal);
            if (start < 0) continue;

            var macro = href[(start + Prefix.Length)..];
            var question = macro.IndexOf('?', StringComparison.Ordinal);
            if (question >= 0) macro = macro[..question];
            if (macro.Length > 0) events[raised] = macro;
        }
        return events;
    }
}

internal sealed class FormControlCodec
{
    private readonly OdfControlKinds _kinds = new();
    private readonly OdfFormVocabulary _vocabulary = new();
    private readonly FormSettings _settings = new();
    private readonly FormListeners _listeners;
    private readonly OdfCellBindings _bindings = new();

    public FormControlCodec() => _listeners = new FormListeners(_vocabulary);

    public XElement Build(string id, FormControlObject control) => Build(id, control, null);

    public XElement Build(string id, FormControlObject control, string? sheetName)
    {
        var properties = control.Properties;
        var element = new XElement(Namespaces.Form + _kinds.Of(control.ControlKind).Element,
            new XAttribute(Namespaces.Xml + "id", System.Xml.XmlConvert.EncodeLocalName(id)),
            new XAttribute(Namespaces.Form + "id", id),
            new XAttribute(Namespaces.Form + "name", properties.Name),
            new XAttribute(Namespaces.Form + "control-implementation", _kinds.Implementation(control.ControlKind)),
            new XAttribute(Namespaces.Form + "disabled", properties.Enabled ? "false" : "true"),
            new XAttribute(Namespaces.Form + "printable", properties.Printable ? "true" : "false"),
            new XAttribute(Namespaces.Form + "tab-stop", properties.TabStop ? "true" : "false"),
            new XAttribute(Namespaces.Form + "tab-index", properties.TabIndex.ToString(CultureInfo.InvariantCulture)));

        if (properties.Label is { } label) element.SetAttributeValue(Namespaces.Form + "label", label);
        if (properties.Tooltip is { } tip) element.SetAttributeValue(Namespaces.Form + "title", tip);
        if (properties.ReadOnly) element.SetAttributeValue(Namespaces.Form + "readonly", "true");
        if (properties.DataField is { Length: > 0 } field) element.SetAttributeValue(Namespaces.Form + "data-field", field);

        if (properties.Value.Checked != CheckState.Unchecked)
        {
            var state = _vocabulary.States.Token(properties.Value.Checked);
            element.SetAttributeValue(Namespaces.Form + "state", state);
            element.SetAttributeValue(Namespaces.Form + "current-state", state);
        }

        if (properties.Value.Text is { } text)
        {
            element.SetAttributeValue(Namespaces.Form + "value", text);
            element.SetAttributeValue(Namespaces.Form + "current-value", text);
        }

        foreach (var item in properties.Items)
            element.Add(new XElement(Namespaces.Form + "option", new XAttribute(Namespaces.Form + "label", item), item));

        if (sheetName is not null) _bindings.Write(element, control, sheetName);

        var bag = new FormPropertyBag();
        var context = new FormSettingContext(control.ControlKind, bag);
        foreach (var setting in _settings.All) setting.Write(properties, context);
        element.Add(bag.Element, _listeners.Build(properties));

        return element;
    }

    public FormControlKind? KindOf(XElement element)
    {
        if (new FormPropertyBag(element).Number(KindSetting.Name) is { } stored && Enum.IsDefined(typeof(FormControlKind), (int)stored))
            return (FormControlKind)(int)stored;

        return _kinds.FromImplementation(element.Attribute(Namespaces.Form + "control-implementation")?.Value)
            ?? _kinds.FromElement(element.Name.LocalName);
    }

    public FormControlProperties Read(XElement element, string? ownSheet = null)
    {
        var bag = new FormPropertyBag(element);
        string? Attr(string name) => element.Attribute(Namespaces.Form + name)?.Value;

        var properties = new FormControlProperties
        {
            Name = Attr("name") ?? string.Empty,
            Label = Attr("label"),
            Tooltip = Attr("title"),
            Enabled = Attr("disabled") != "true",
            Printable = Attr("printable") != "false",
            ReadOnly = Attr("readonly") == "true",
            DataField = Attr("data-field"),
            TabStop = Attr("tab-stop") != "false",
            TabIndex = int.TryParse(Attr("tab-index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : 0,
            Items = [.. element.Elements(Namespaces.Form + "option").Select(option => option.Attribute(Namespaces.Form + "label")?.Value ?? option.Value)],
            Events = _listeners.Read(element),
            Text = new FormTextStyle(),
            Value = new FormControlValue
            {
                Text = Attr("current-value") ?? Attr("value"),
                Checked = _vocabulary.States.Value(Attr("current-state") ?? Attr("state")),
            },
        };

        return _bindings.Read(element, _settings.All.Aggregate(properties, (current, setting) => setting.Read(current, bag)), ownSheet);
    }
}

internal sealed class OdfCellBindings
{
    private readonly OdfQualifiedRanges _ranges = new();

    private readonly SheetReferenceText _references = new();

    public void Write(XElement element, FormControlObject control, string sheetName)
    {
        var properties = control.Properties;

        if (properties.LinkedCell is { } linked && _references.Split(linked) is var link && CellAddress.TryParseA1(link.Local, out var cell))
        {
            element.SetAttributeValue(Namespaces.Form + "linked-cell", _ranges.WriteAddress(link.Sheet ?? sheetName, cell));
            if (control.ControlKind is FormControlKind.ListBox)
                element.SetAttributeValue(Namespaces.Form + "list-linkage-type", "selection-indexes");
        }

        if (properties.SourceRange is { } source && _references.Split(source) is var items && CellRange.TryParseA1(items.Local, out var range))
            element.SetAttributeValue(Namespaces.Form + "source-cell-range", _ranges.Write(items.Sheet ?? sheetName, range));
    }

    public FormControlProperties Read(XElement element, FormControlProperties properties, string? ownSheet)
    {
        if (properties.LinkedCell is null && Range(element, "linked-cell") is { } cell)
            properties = properties with { LinkedCell = _references.Qualify(cell.Sheet, ownSheet, cell.Range.Start.ToA1()) };

        if (properties.SourceRange is null && Range(element, "source-cell-range") is { } range)
            properties = properties with { SourceRange = _references.Qualify(range.Sheet, ownSheet, range.Range.ToString()) };

        return properties;
    }

    private (string Sheet, CellRange Range)? Range(XElement element, string name) =>
        element.Attribute(Namespaces.Form + name)?.Value is { Length: > 0 } text
            ? _ranges.Read(text.Replace("$", string.Empty, StringComparison.Ordinal))
            : null;
}
