using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class GraphicStyleInheritanceTests
{
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    [Fact]
    public void IlRiempimentoDelloStileStandardEsternoSiEreditaSeLaFormaNonLoRidefinisce()
    {
        var inheritance = new OdfGraphicStyleInheritance();
        var standard = new XElement(Style + "style", new XAttribute(Style + "name", "standard"),
            new XElement(Style + "graphic-properties", new XAttribute(Draw + "fill", "solid"), new XAttribute(Draw + "fill-color", "#729fcf")));
        var automatic = new XElement(Style + "style", new XAttribute(Style + "name", "gr1"), new XAttribute(Style + "parent-style-name", "standard"),
            new XElement(Style + "graphic-properties", new XAttribute(Draw + "textarea-vertical-align", "middle")));
        var definitions = new Dictionary<string, XElement> { [inheritance.Key("standard")] = standard };

        var style = new OdfShapeStyleReader().Read(automatic, definitions);

        Assert.Equal("#729fcf", style.Fill.ColorHex, ignoreCase: true);
    }
}
