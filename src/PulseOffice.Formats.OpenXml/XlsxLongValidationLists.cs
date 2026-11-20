using System.Xml.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using DataValidation = DocumentFormat.OpenXml.Spreadsheet.DataValidation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxLongValidationLists
{
    public const int FormulaLimit = 255;

    private const string Uri = "{3E9D4B27-6A1C-4F58-B2E0-91C7D5A86F43}";

    private readonly XNamespace _main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

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
