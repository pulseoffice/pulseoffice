using System.Xml.Linq;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfProtectionMapper
{
    private const string Sha256 = "http://www.w3.org/2000/09/xmldsig#sha256";
    private const string Unreadable = "unreadable";

    public void Write(Worksheet sheet, XElement table)
    {
        if (!sheet.Protection.IsEnabled) return;

        table.Add(new XAttribute(Namespaces.Table + "protected", "true"));
        Key(table, sheet.Protection.PasswordHash);

        var options = new XElement(Namespaces.Loext + "table-protection");
        foreach (var option in Options)
            if (option.Get(sheet.Protection)) options.Add(new XAttribute(Namespaces.Loext + option.Token, "true"));
        table.AddFirst(options);
    }

    private sealed record ProtectionOption(string Token, Func<SheetProtection, bool> Get, Func<SheetProtection, bool, SheetProtection> Set);

    private readonly ProtectionOption[] Options =
    [
        new("select-protected-cells", p => p.AllowSelectLockedCells, (p, v) => p with { AllowSelectLockedCells = v }),
        new("select-unprotected-cells", p => p.AllowSelectUnlockedCells, (p, v) => p with { AllowSelectUnlockedCells = v }),
        new("insert-columns", p => p.AllowInsertColumns, (p, v) => p with { AllowInsertColumns = v }),
        new("insert-rows", p => p.AllowInsertRows, (p, v) => p with { AllowInsertRows = v }),
        new("delete-columns", p => p.AllowDeleteColumns, (p, v) => p with { AllowDeleteColumns = v }),
        new("delete-rows", p => p.AllowDeleteRows, (p, v) => p with { AllowDeleteRows = v }),
        new("use-autofilter", p => p.AllowFilter, (p, v) => p with { AllowFilter = v }),
    ];

    public void Write(Workbook workbook, XElement spreadsheet)
    {
        if (!workbook.Protection.Structure) return;

        spreadsheet.Add(new XAttribute(Namespaces.Table + "structure-protected", "true"));
        Key(spreadsheet, workbook.Protection.PasswordHash);
    }

    public void Read(XElement table, Worksheet sheet)
    {
        if ((string?)table.Attribute(Namespaces.Table + "protected") != "true") return;

        var protection = sheet.Protection with { IsEnabled = true, PasswordHash = HashOf(table) };
        var options = table.Element(Namespaces.Loext + "table-protection")
            ?? table.Element(Namespaces.Table + "table-protection");
        if (options is not null)
            foreach (var option in Options)
                protection = option.Set(protection, options.Attributes().Any(attribute => attribute.Name.LocalName == option.Token && attribute.Value == "true"));
        sheet.Protection = protection;
    }

    public void Read(XElement spreadsheet, Workbook workbook)
    {
        if ((string?)spreadsheet.Attribute(Namespaces.Table + "structure-protected") != "true") return;

        workbook.Protection = workbook.Protection with { Structure = true, PasswordHash = HashOf(spreadsheet) };
    }

    public void Key(XElement element, string? hash)
    {
        if (hash is not { Length: > 0 } || hash == Unreadable) return;
        if (hash.StartsWith(PasswordKey.OdfPrefix, StringComparison.Ordinal))
        {
            var parts = hash[PasswordKey.OdfPrefix.Length..].Split('|');
            if (parts.Length is < 2 or > 3) return;
            element.Add(new XAttribute(Namespaces.Table + "protection-key", parts[^1]));
            if (parts[0].Length > 0) element.Add(new XAttribute(Namespaces.Table + "protection-key-digest-algorithm", parts[0]));
            if (parts.Length == 3 && parts[1].Length > 0) element.Add(new XAttribute(Namespaces.Loext + "protection-key-digest-algorithm-2", parts[1]));
            return;
        }
        if (hash.StartsWith(PasswordKey.OoxmlPrefix, StringComparison.Ordinal) || hash.StartsWith(PasswordKey.OoxmlLegacyPrefix, StringComparison.Ordinal)) return;

        element.Add(
            new XAttribute(Namespaces.Table + "protection-key", hash),
            new XAttribute(Namespaces.Table + "protection-key-digest-algorithm", Sha256));
    }

    public string? HashOf(XElement element)
    {
        if ((string?)element.Attribute(Namespaces.Table + "protection-key") is not { Length: > 0 } key) return null;

        var algorithm = (string?)element.Attribute(Namespaces.Table + "protection-key-digest-algorithm") ?? string.Empty;
        var second = (string?)element.Attribute(Namespaces.Loext + "protection-key-digest-algorithm-2") ?? string.Empty;
        return algorithm == Sha256 && second.Length == 0 ? key : $"{PasswordKey.OdfPrefix}{algorithm}|{second}|{key}";
    }
}
