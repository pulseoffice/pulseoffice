using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Forms;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class FormPropertyBag
{
    private readonly List<XElement> _written = [];
    private readonly Dictionary<string, XElement> _read;

    public FormPropertyBag() => _read = new Dictionary<string, XElement>(StringComparer.Ordinal);

    public FormPropertyBag(XElement element) =>
        _read = element.Elements(Namespaces.Form + "properties").Elements(Namespaces.Form + "property")
            .GroupBy(property => property.Attribute(Namespaces.Form + "property-name")?.Value ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public XElement? Element => _written.Count == 0 ? null : new XElement(Namespaces.Form + "properties", _written);

    public void Text(string name, string? value)
    {
        if (value is not null) Add(name, "string", Namespaces.Office + "string-value", value);
    }

    public void Flag(string name, bool value) => Add(name, "boolean", Namespaces.Office + "boolean-value", value ? "true" : "false");

    public void Number(string name, double value) =>
        Add(name, "float", Namespaces.Office + "value", value.ToString(CultureInfo.InvariantCulture));

    public string? Text(string name) =>
        _read.TryGetValue(name, out var property) && property.Attribute(Namespaces.Office + "string-value")?.Value is { Length: > 0 } value
            ? value
            : null;

    public bool? Flag(string name) =>
        _read.TryGetValue(name, out var property) ? property.Attribute(Namespaces.Office + "boolean-value")?.Value == "true" : null;

    public double? Number(string name) =>
        _read.TryGetValue(name, out var property)
        && double.TryParse(property.Attribute(Namespaces.Office + "value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && double.IsFinite(value)
            ? value
            : null;

    private void Add(string name, string type, XName attribute, string value) =>
        _written.Add(new XElement(Namespaces.Form + "property",
            new XAttribute(Namespaces.Form + "property-name", name),
            new XAttribute(Namespaces.Office + "value-type", type),
            new XAttribute(attribute, value)));
}

internal sealed record FormSettingContext(FormControlKind Kind, FormPropertyBag Bag);

internal abstract class FormSetting
{
    public abstract void Write(FormControlProperties properties, FormSettingContext context);

    public abstract FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag);
}

internal sealed class TextSetting(string name, Func<FormControlProperties, string?> get, Func<FormControlProperties, string?, FormControlProperties> set)
    : FormSetting
{
    public override void Write(FormControlProperties properties, FormSettingContext context) => context.Bag.Text(name, get(properties));

    public override FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag) => set(properties, bag.Text(name));
}

internal sealed record SettingBounds(double Minimum, double Maximum)
{
    public double Clamp(double value) => Math.Clamp(value, Minimum, Maximum);
}

internal sealed class NumberSetting(string name, double fallback, SettingBounds bounds, Func<FormControlProperties, double> get, Func<FormControlProperties, double, FormControlProperties> set)
    : FormSetting
{
    public override void Write(FormControlProperties properties, FormSettingContext context) => context.Bag.Number(name, get(properties));

    public override FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag) => set(properties, bounds.Clamp(bag.Number(name) ?? fallback));
}

internal sealed class EnumSetting<TValue>(string name, TValue fallback, Func<FormControlProperties, TValue> get, Func<FormControlProperties, TValue, FormControlProperties> set)
    : FormSetting where TValue : struct, Enum
{
    public override void Write(FormControlProperties properties, FormSettingContext context) => context.Bag.Number(name, Convert.ToDouble(get(properties), CultureInfo.InvariantCulture));

    public override FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag) =>
        set(properties, bag.Number(name) is { } stored && stored == Math.Floor(stored) && stored is >= int.MinValue and <= int.MaxValue
            && Enum.ToObject(typeof(TValue), (int)stored) is TValue value && Enum.IsDefined(value) ? value : fallback);
}

internal sealed class FlagSetting(string name, bool fallback, Func<FormControlProperties, bool> get, Func<FormControlProperties, bool, FormControlProperties> set, bool writeOnlyWhenDiffers = false)
    : FormSetting
{
    public override void Write(FormControlProperties properties, FormSettingContext context)
    {
        var value = get(properties);
        if (!writeOnlyWhenDiffers || value != fallback) context.Bag.Flag(name, value);
    }

    public override FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag) => set(properties, bag.Flag(name) ?? fallback);
}

internal sealed class PasswordSetting : FormSetting
{
    public override void Write(FormControlProperties properties, FormSettingContext context)
    {
        if (properties.PasswordCharacter is { } password) context.Bag.Number("EchoChar", password);
    }

    public override FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag) =>
        properties with { PasswordCharacter = bag.Number("EchoChar") is { } echo and > 0 and <= char.MaxValue && !char.IsSurrogate((char)echo) ? (char)echo : null };
}

internal sealed class RangeSetting : FormSetting
{
    public override void Write(FormControlProperties properties, FormSettingContext context)
    {
        var bag = context.Bag;
        if (properties.Range is { } range)
        {
            var spin = context.Kind == FormControlKind.SpinButton;
            var prefix = spin ? "Spin" : "Scroll";
            bag.Number(prefix + "ValueMin", range.Minimum);
            bag.Number(prefix + "ValueMax", range.Maximum);
            bag.Number(spin ? "SpinIncrement" : "LineIncrement", range.Step);
            if (properties.Value.Number is { } value) bag.Number(prefix + "Value", value);
        }
        else if (properties.Value.Number is { } value)
        {
            bag.Number("EffectiveValue", value);
        }
    }

    public override FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag)
    {
        var minimum = bag.Number("ScrollValueMin") ?? bag.Number("SpinValueMin");
        var maximum = bag.Number("ScrollValueMax") ?? bag.Number("SpinValueMax");
        var step = bag.Number("LineIncrement") ?? bag.Number("SpinIncrement");
        var number = bag.Number("ScrollValue") ?? bag.Number("SpinValue") ?? bag.Number("EffectiveValue");

        return properties with
        {
            Range = minimum is { } low && maximum is { } high ? new NumericRange(Math.Min(low, high), Math.Max(low, high), step is > 0 and var positive ? positive : 1, 10) : null,
            Value = properties.Value with { Number = number },
        };
    }
}

internal sealed class KindSetting : FormSetting
{
    public const string Name = "PulseControlKind";

    public override void Write(FormControlProperties properties, FormSettingContext context) => context.Bag.Number(Name, (double)context.Kind);

    public override FormControlProperties Read(FormControlProperties properties, FormPropertyBag bag) => properties;
}

internal sealed class FormSettings
{
    public IReadOnlyList<FormSetting> All { get; } =
    [
        new TextSetting("LinkedCell", p => p.LinkedCell, (p, v) => p with { LinkedCell = v }),
        new TextSetting("SourceCellRange", p => p.SourceRange, (p, v) => p with { SourceRange = v }),
        new TextSetting("GroupName", p => p.Group, (p, v) => p with { Group = v }),
        new TextSetting("HelpText", p => p.HelpText, (p, v) => p with { HelpText = v }),
        new TextSetting("BackgroundColor", p => p.BackgroundHex, (p, v) => p with { BackgroundHex = v }),
        new TextSetting("BorderColor", p => p.BorderHex, (p, v) => p with { BorderHex = v }),
        new TextSetting("FontName", p => p.Text.Family, (p, v) => p with { Text = p.Text with { Family = v ?? "Helvetica" } }),
        new TextSetting("TextColor", p => p.Text.ColorHex, (p, v) => p with { Text = p.Text with { ColorHex = v ?? "#0F172A" } }),
        new TextSetting("CurrencySymbol", p => p.CurrencySymbol, (p, v) => p with { CurrencySymbol = v }),
        new TextSetting("TargetURL", p => p.TargetUrl, (p, v) => p with { TargetUrl = v }),
        new TextSetting("ImageURL", p => p.ImagePath, (p, v) => p with { ImagePath = v }),

        new NumberSetting("FontHeight", 11, new SettingBounds(1, 4000), p => p.Text.Size, (p, v) => p with { Text = p.Text with { Size = v } }),
        new NumberSetting("FontWeight", 100, new SettingBounds(0, 1000), p => p.Text.Bold ? 150 : 100, (p, v) => p with { Text = p.Text with { Bold = v >= 150 } }),
        new EnumSetting<FormTextAlignment>("Align", FormTextAlignment.Left, p => p.Text.Alignment, (p, v) => p with { Text = p.Text with { Alignment = v } }),
        new EnumSetting<FormBorderKind>("Border", FormBorderKind.Flat, p => p.Border, (p, v) => p with { Border = v }),
        new NumberSetting("MaxTextLen", 0, new SettingBounds(0, int.MaxValue), p => p.MaxTextLength, (p, v) => p with { MaxTextLength = (int)v }),
        new NumberSetting("DecimalAccuracy", 2, new SettingBounds(0, 20), p => p.DecimalPlaces, (p, v) => p with { DecimalPlaces = (int)v }),
        new NumberSetting("BoundColumn", 1, new SettingBounds(-1, 1024), p => p.BoundColumn, (p, v) => p with { BoundColumn = (int)v }),
        new NumberSetting("LineCount", 8, new SettingBounds(1, 1000), p => p.DropdownLines, (p, v) => p with { DropdownLines = (int)v }),
        new EnumSetting<FormButtonAction>("ButtonType", FormButtonAction.None, p => p.ButtonAction, (p, v) => p with { ButtonAction = v }),
        new EnumSetting<FormOrientation>("Orientation", FormOrientation.Horizontal, p => p.Orientation, (p, v) => p with { Orientation = v }),
        new NumberSetting("RepeatDelay", 50, new SettingBounds(0, 60_000), p => p.RepeatDelayMilliseconds, (p, v) => p with { RepeatDelayMilliseconds = (int)v }),
        new EnumSetting<FormImageScale>("ImageScaleMode", FormImageScale.Fit, p => p.ImageScale, (p, v) => p with { ImageScale = v }),

        new FlagSetting("FontItalic", false, p => p.Text.Italic, (p, v) => p with { Text = p.Text with { Italic = v } }),
        new FlagSetting("FontUnderline", false, p => p.Text.Underline, (p, v) => p with { Text = p.Text with { Underline = v } }),
        new FlagSetting("MultiLine", false, p => p.MultiLine, (p, v) => p with { MultiLine = v }),
        new FlagSetting("ShowThousandsSeparator", false, p => p.ThousandsSeparator, (p, v) => p with { ThousandsSeparator = v }),
        new FlagSetting("Dropdown", true, p => p.Dropdown, (p, v) => p with { Dropdown = v }),
        new FlagSetting("MultiSelection", false, p => p.MultiSelect, (p, v) => p with { MultiSelect = v }),
        new FlagSetting("Toggle", false, p => p.Toggle, (p, v) => p with { Toggle = v }),
        new FlagSetting("Spin", false, p => p.Spin, (p, v) => p with { Spin = v }),

        new PasswordSetting(),
        new FlagSetting("EnableVisible", true, p => p.Visible, (p, v) => p with { Visible = v }, writeOnlyWhenDiffers: true),
        new FlagSetting("TriState", false, p => p.TriState, (p, v) => p with { TriState = v }, writeOnlyWhenDiffers: true),
        new RangeSetting(),
        new KindSetting(),
    ];
}
