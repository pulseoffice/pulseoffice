using System.Xml.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DataValidation = DocumentFormat.OpenXml.Spreadsheet.DataValidation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxValidationExtension(X14Namespaces names)
{
    public const string Uri = "{CCE6A557-97BC-4b89-ADB6-D9C93CAAB3DF}";

    private readonly IReadOnlyList<string> _formulas = ["formula1", "formula2"];

    public IEnumerable<DataValidation> Read(WorksheetExtensionList? extensions)
    {
        if (extensions is null) yield break;

        foreach (var extension in extensions.Elements<WorksheetExtension>().Where(extension => extension.Uri?.Value == Uri))
        {
            XElement root;
            try { root = XElement.Parse(extension.OuterXml); }
            catch (System.Xml.XmlException) { continue; }

            foreach (var validation in root.Descendants(names.X14 + "dataValidation"))
                yield return new DataValidation(Translate(validation).ToString(SaveOptions.DisableFormatting));
        }
    }

    private XElement Translate(XElement validation)
    {
        var element = new XElement(names.Main + "dataValidation",
            validation.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.Namespace == XNamespace.None));
        foreach (var name in _formulas)
            if (validation.Element(names.X14 + name)?.Element(names.Xm + "f") is { } formula)
                element.Add(new XElement(names.Main + name, formula.Value));
        element.SetAttributeValue("sqref", (string?)validation.Element(names.Xm + "sqref") ?? string.Empty);
        return element;
    }
}
