using DocumentFormat.OpenXml.Packaging;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlReadFailures
{
    public bool IsMalformedInput(Exception exception) =>
        exception is InvalidDataException or IOException or OpenXmlPackageException or System.Xml.XmlException
            or FormatException or ArgumentException or InvalidOperationException or ArithmeticException or NotSupportedException;
}
