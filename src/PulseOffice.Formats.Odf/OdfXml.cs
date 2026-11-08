using System.Xml.Linq;

namespace PulseOffice.Formats.Odf;

internal abstract class OdfXml
{
    protected static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    protected static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    protected static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    protected static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    protected static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    protected static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    protected static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    protected static readonly XNamespace Dc = OdfPackage.Namespaces.Dc;
    protected static readonly XNamespace Smil = OdfPackage.Namespaces.Smil;
    protected static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;
    protected static readonly XNamespace Anim = OdfPackage.Namespaces.Anim;
    protected static readonly XNamespace Xml = OdfPackage.Namespaces.Xml;
    protected static readonly XNamespace Form = OdfPackage.Namespaces.Form;
}
