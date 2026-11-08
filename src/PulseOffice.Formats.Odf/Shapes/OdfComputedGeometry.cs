using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;
using PulseOffice.Foundation.Units;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfComputedGeometry(IShapePresets presets, IPresetFormulaTranslator translator, IEnhancedPathInterpreter interpreter, INativeShapeModifiers native, INativeShapeDefaults defaults)
{
    private const double HundredthsPerTwip = 2540.0 / 1440.0;
    private const string GenericType = "non-primitive";

    private readonly XName _geometry = Namespaces.Draw + "enhanced-geometry";
    private readonly XName _equation = Namespaces.Draw + "equation";
    private readonly XName _handle = Namespaces.Draw + "handle";

    public OdfComputedGeometry() : this(new ShapePresets(), new PresetFormulaTranslator(), new EnhancedPathInterpreter(), new NativeShapeModifiers(), new NativeShapeDefaults())
    {
    }

    public double Hundredths(Twip length) => length.Value * HundredthsPerTwip;

    public bool Maps(string? type) => native.Maps(type);

    public bool Evaluates(XElement customShape) => Read(customShape.Element(_geometry), 1000, 1000) is not null;

    public ShapeObject Apply(ShapeObject shape, XElement? geometry, Twip width, Twip height) =>
        Read(geometry, Hundredths(width), Hundredths(height)) is { } computed
            ? shape with { Shape = presets.Knows((string?)geometry!.Attribute(Namespaces.Draw + "type")) ? shape.Shape : ShapeKind.Rectangle, Computed = computed }
            : shape;

    public ComputedGeometry? Read(XElement? geometry, double width, double height)
    {
        if (geometry is null || (string?)geometry.Attribute(Namespaces.Draw + "text-path") == "true") return null;
        var type = (string?)geometry.Attribute(Namespaces.Draw + "type");
        var modifiers = Numbers((string?)geometry.Attribute(Namespaces.Draw + "modifiers"));
        if (native.Preset(type, modifiers, width, height) is { } mapped)
            return presets.Knows(type) && !translator.Adjusted(mapped) ? null : mapped;
        if (presets.Knows(type))
        {
            if (modifiers.Count > 0 && translator.Preset(type, modifiers, width, height) is { } adjusted && translator.Adjusted(adjusted)) return adjusted;
            if (modifiers.Count == 0 || defaults.AreDefault(type, modifiers) || geometry.Element(_equation) is null) return null;
        }
        else if (translator.Preset(type, modifiers, width, height) is { } preset) return preset;
        var path = (string?)geometry.Attribute(Namespaces.Draw + "enhanced-path");
        if (string.IsNullOrWhiteSpace(path)) return null;
        var view = Numbers((string?)geometry.Attribute(Namespaces.Svg + "viewBox"));
        var formula = new FormulaGeometry(path)
        {
            Type = type,
            ViewLeft = view.Count == 4 ? view[0] : 0,
            ViewTop = view.Count == 4 ? view[1] : 0,
            ViewWidth = view.Count == 4 && view[2] > 0 ? view[2] : 21600,
            ViewHeight = view.Count == 4 && view[3] > 0 ? view[3] : 21600,
            Modifiers = modifiers,
            Equations = [.. geometry.Elements(_equation).Select(equation =>
                new EnhancedEquation((string?)equation.Attribute(Namespaces.Draw + "name") ?? string.Empty, (string?)equation.Attribute(Namespaces.Draw + "formula") ?? "0"))],
            Handles = [.. geometry.Elements(_handle).Select(handle =>
                new EnhancedHandle([.. handle.Attributes().Where(attribute => attribute.Name.Namespace == Namespaces.Draw)
                    .Select(attribute => new KeyValuePair<string, string>(attribute.Name.LocalName, attribute.Value))]))],
            TextAreas = (string?)geometry.Attribute(Namespaces.Draw + "text-areas"),
            ReferenceWidth = width > 0 ? width : 1,
            ReferenceHeight = height > 0 ? height : 1,
        };
        return interpreter.Evaluates(formula) ? formula : null;
    }

    public void Write(XElement geometry, ShapeObject shape, Twip width, Twip height)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(shape);
        var formula = shape.Computed switch
        {
            FormulaGeometry own => own,
            PresetGeometry preset => translator.Translate(preset,
                width.Value > 0 ? Hundredths(width) : preset.ReferenceWidth, height.Value > 0 ? Hundredths(height) : preset.ReferenceHeight),
            _ => null,
        };
        if (formula is null) return;
        geometry.SetAttributeValue(Namespaces.Draw + "type", formula.Type ?? GenericType);
        geometry.SetAttributeValue(Namespaces.Svg + "viewBox", string.Join(' ', new[] { formula.ViewLeft, formula.ViewTop, formula.ViewWidth, formula.ViewHeight }.Select(Number)));
        geometry.SetAttributeValue(Namespaces.Draw + "enhanced-path", formula.Path);
        geometry.SetAttributeValue(Namespaces.Draw + "modifiers", formula.Modifiers.Count > 0 ? string.Join(' ', formula.Modifiers.Select(Number)) : null);
        geometry.SetAttributeValue(Namespaces.Draw + "text-areas", formula.TextAreas);
        geometry.Elements(_equation).Remove();
        geometry.Elements(_handle).Remove();
        geometry.Add(formula.Equations.Select(equation => new XElement(_equation,
            new XAttribute(Namespaces.Draw + "name", equation.Name), new XAttribute(Namespaces.Draw + "formula", equation.Formula))));
        geometry.Add(formula.Handles.Select(handle => new XElement(_handle,
            handle.Attributes.Select(attribute => new XAttribute(Namespaces.Draw + attribute.Key, attribute.Value)))));
    }

    private string Number(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);

    private List<double> Numbers(string? text) =>
        [.. (text ?? string.Empty).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0)];
}
