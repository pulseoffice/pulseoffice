using DocumentFormat.OpenXml;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class PresetColors
{
    private readonly IReadOnlyList<(string Short, string Long)> _prefixes = [("dk", "Dark"), ("lt", "Light"), ("med", "Medium")];

    public uint? Of(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var expanded = _prefixes.FirstOrDefault(prefix => name.StartsWith(prefix.Short, StringComparison.Ordinal)
            && name.Length > prefix.Short.Length && char.IsUpper(name[prefix.Short.Length])) is { Short: not null } found
            ? found.Long + name[found.Short.Length..]
            : name;
        var colour = System.Drawing.Color.FromName(expanded);
        return colour.IsKnownColor && !colour.IsSystemColor ? 0xFF000000 | (uint)(colour.ToArgb() & 0xFFFFFF) : null;
    }
}

internal interface IDrawingColorReader
{
    uint? Read(OpenXmlElement? container);
}

internal sealed class RunColorReader(OoxmlValues values, SchemeRoles schemes, PresetColors presets) : IDrawingColorReader
{
    public uint? Read(OpenXmlElement? container)
    {
        if (container is null) return null;
        if (container.GetFirstChild<D.RgbColorModelHex>()?.Val?.Value is { Length: 6 } hex) return values.Color(hex);
        if (container.GetFirstChild<D.SchemeColor>()?.Val?.Value is { } scheme) return schemes.Absolute(scheme);
        if (container.GetFirstChild<D.PresetColor>() is { } preset) return presets.Of(preset.Val?.InnerText);
        if (container.GetFirstChild<D.SystemColor>()?.LastColor?.Value is { Length: 6 } last) return values.Color(last);
        return null;
    }
}
