using System.Globalization;
using System.Text.RegularExpressions;

namespace PulseOffice.Formats;

public interface IAnimationAttributeTable
{
    string ToOoxml(string odfAttribute);

    string FromOoxml(string ooxmlAttribute);

    string ValueToOoxml(string odfAttribute, string value);

    string ValueFromOoxml(string odfAttribute, string value);

    bool IsNumber(string value);
}

public sealed class AnimationAttributeTable : IAnimationAttributeTable
{
    private readonly IReadOnlyList<(string Odf, string Ooxml)> _names =
    [
        ("x", "ppt_x"),
        ("y", "ppt_y"),
        ("width", "ppt_w"),
        ("height", "ppt_h"),
        ("dim", "ppt_c"),
        ("rotate", "r"),
        ("rotate", "style.rotation"),
        ("skewX", "xshear"),
        ("fill-color", "fillcolor"),
        ("fill-color", "fillColor"),
        ("fill", "fill.type"),
        ("fill-on", "fill.on"),
        ("stroke-color", "stroke.color"),
        ("stroke", "stroke.on"),
        ("color", "style.color"),
        ("font-weight", "style.fontWeight"),
        ("text-underline", "style.textDecorationUnderline"),
        ("font-family", "style.fontFamily"),
        ("font-size", "style.fontSize"),
        ("font-style", "style.fontStyle"),
        ("visibility", "style.visibility"),
        ("opacity", "style.opacity"),
    ];

    private readonly IReadOnlyList<(string Odf, string Ooxml)> _strokeValues = [("solid", "true"), ("none", "false")];

    private readonly Regex _odfMeasure = new(@"(?<![#\w.])(x|y|width|height)(?![\w(])", RegexOptions.CultureInvariant);

    private readonly Regex _ooxmlMeasure = new(@"#?ppt_(x|y|w|h)\b", RegexOptions.CultureInvariant);

    public string ToOoxml(string odfAttribute) =>
        _names.FirstOrDefault(pair => pair.Odf == odfAttribute).Ooxml ?? odfAttribute;

    public string FromOoxml(string ooxmlAttribute) =>
        _names.FirstOrDefault(pair => pair.Ooxml == ooxmlAttribute).Odf ?? ooxmlAttribute;

    public string ValueToOoxml(string odfAttribute, string value)
    {
        if (odfAttribute == "stroke")
            return _strokeValues.FirstOrDefault(pair => pair.Odf == value).Ooxml ?? value;
        return _odfMeasure.Replace(value, match => match.Value switch
        {
            "x" => "#ppt_x",
            "y" => "#ppt_y",
            "width" => "#ppt_w",
            _ => "#ppt_h",
        });
    }

    public string ValueFromOoxml(string odfAttribute, string value)
    {
        if (odfAttribute == "stroke")
            return _strokeValues.FirstOrDefault(pair => pair.Ooxml == value).Odf ?? value;
        return _ooxmlMeasure.Replace(value, match => match.Groups[1].Value switch
        {
            "x" => "x",
            "y" => "y",
            "w" => "width",
            _ => "height",
        });
    }

    public bool IsNumber(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number);
}
