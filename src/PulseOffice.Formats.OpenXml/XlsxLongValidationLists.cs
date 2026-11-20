using System.Xml.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DataValidation = DocumentFormat.OpenXml.Spreadsheet.DataValidation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxLongValidationLists
{
    public const int FormulaLimit = 255;

    private const string Uri = "{3E9D4B27-6A1C-4F58-B2E0-91C7D5A86F43}";

    private readonly XNamespace _ours = "urn:pulseoffice:spreadsheet";

    private readonly XNamespace _main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public bool IsOverlong(string? literal) => literal is not null && literal.Length - 2 > FormulaLimit;

    public WorksheetExtension? Extension(IReadOnlyList<DataValidation> validations)
    {
        if (validations.Count == 0) return null;
        var container = new XElement(_ours + "dataValidations",
            new XAttribute(XNamespace.Xmlns + "pt", _ours.NamespaceName),
            validations.Select(validation => XElement.Parse(validation.OuterXml)));
        return new WorksheetExtension { Uri = Uri, InnerXml = container.ToString(SaveOptions.DisableFormatting) };
    }

    public IEnumerable<DataValidation> Read(WorksheetExtensionList? extensions)
    {
        if (extensions is null) yield break;

        foreach (var extension in extensions.Elements<WorksheetExtension>().Where(extension => extension.Uri?.Value == Uri))
        {
            XElement root;
            try { root = XElement.Parse(extension.OuterXml); }
            catch (System.Xml.XmlException) { continue; }

            foreach (var validation in root.Descendants(_main + "dataValidation"))
                yield return new DataValidation(validation.ToString(SaveOptions.DisableFormatting));
        }
    }
}
