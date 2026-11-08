using System.Collections.Concurrent;
using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlAttributeSanitizer
{
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _attributes = new();

    public void Sanitize(OpenXmlPackage package) => Sanitize(package, _ => false);

    public void Sanitize(OpenXmlPackage package, Func<OpenXmlPart, bool> streamed)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(streamed);
        foreach (var part in package.GetAllParts())
        {
            if (!IsXml(part) || streamed(part)) continue;
            if (part.RootElement is not { } root) continue;
            SanitizeTree(root);
        }
    }

    public void SanitizeTree(OpenXmlElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Sanitize(root);
        foreach (var element in root.Descendants()) Sanitize(element);
    }

    private bool IsXml(OpenXmlPart part) =>
        part.ContentType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)
        || part.ContentType.EndsWith("/xml", StringComparison.OrdinalIgnoreCase);

    private void Sanitize(OpenXmlElement element)
    {
        if (!element.HasAttributes) return;
        foreach (var property in _attributes.GetOrAdd(element.GetType(), TypedAttributes))
            if (property.GetValue(element) is OpenXmlSimpleType value && !value.HasValue && !Repaired(value))
                property.SetValue(element, null);
    }

    private bool Repaired(OpenXmlSimpleType value)
    {
        if (value.InnerText is not { Length: > 1 } text || !text.EndsWith('%')
            || !double.TryParse(text[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent)
            || !double.IsFinite(percent) || Math.Abs(percent * 1000) > int.MaxValue)
            return false;
        value.InnerText = Math.Round(percent * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value.HasValue;
    }

    private PropertyInfo[] TypedAttributes(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0
                && typeof(OpenXmlSimpleType).IsAssignableFrom(property.PropertyType)
                && property.PropertyType != typeof(StringValue))
            .ToArray();
}
