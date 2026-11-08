using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace PulseOffice.Formats.OpenXml;

internal sealed class MalformedXmlRepair(WorksheetSalvage worksheets) : IPackageRepairStep
{
    private static readonly HashSet<string> Essential = new(StringComparer.OrdinalIgnoreCase)
    {
        PackageModel.ContentTypesPart, "_rels/.rels",
    };

    private readonly XmlReaderSettings _settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    public bool Apply(PackageModel package)
    {
        var changed = false;
        foreach (var name in package.TruncatedNames)
        {
            package.Resolve(name);
            changed |= Settle(package, name, package.Truncated(name) ?? []);
        }

        foreach (var name in package.Names.Where(IsXml).ToList())
        {
            var bytes = package[name]!;
            if (WellFormed(bytes)) continue;
            changed |= Settle(package, name, bytes);
        }

        return changed;
    }

    private bool Settle(PackageModel package, string name, byte[] bytes)
    {
        if (Essential.Contains(name))
        {
            package[name] = bytes;
            return false;
        }

        if (worksheets.Handles(name) && worksheets.Salvage(Encoding.UTF8.GetString(bytes)) is { } salvaged && WellFormed(salvaged))
        {
            package[name] = salvaged;
            package.Damage.Add(new PartDamage(name, PartDamageKind.Recovered));
            return true;
        }

        package[name] = null;
        package.Damage.Add(new PartDamage(name, PartDamageKind.Skipped));
        return true;
    }

    private bool IsXml(string name) =>
        name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".vml", StringComparison.OrdinalIgnoreCase);

    private bool WellFormed(byte[] bytes)
    {
        if (bytes.Length == 0) return false;
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(bytes), _settings);
            while (reader.Read())
            {
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}

internal sealed class WorksheetSalvage
{
    private static readonly Regex WorksheetPart = new(@"^xl/(worksheets|dialogsheets)/[^/]+\.xml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RootTag = new(@"<(?<prefix>\w+:)?worksheet\b[^>]*>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex Declaration = new(@"xmlns(:\w+)?\s*=\s*""[^""]*""", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex RowStart = new(@"<(?:\w+:)?row\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex CellStart = new(@"<(?:\w+:)?c\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    private static readonly Regex RowTag = new(@"^<(?:\w+:)?row\b[^<>]*>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    private readonly XmlReaderSettings _settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    public bool Handles(string name) => WorksheetPart.IsMatch(name);

    public byte[]? Salvage(string text)
    {
        if (RootTag.Match(text) is not { Success: true } root) return null;
        var prefix = root.Groups["prefix"].Value;
        var declarations = string.Join(' ', Declaration.Matches(root.Value).Select(match => match.Value));
        var wrapper = (string body) => $"<{prefix}worksheet {declarations}>{body}</{prefix}worksheet>";

        var dataStart = text.IndexOf($"<{prefix}sheetData", root.Index + root.Length, StringComparison.Ordinal);
        var head = dataStart < 0 ? string.Empty : text[(root.Index + root.Length)..dataStart];
        if (!Parses(wrapper(head))) head = string.Empty;

        var rows = new StringBuilder();
        if (dataStart >= 0)
        {
            var dataEnd = text.IndexOf($"</{prefix}sheetData>", dataStart, StringComparison.Ordinal);
            var data = dataEnd < 0 ? text[dataStart..] : text[dataStart..dataEnd];
            foreach (var row in Chunks(data, RowStart, $"</{prefix}row>"))
                rows.Append(Row(row, prefix, wrapper));
        }

        var rebuilt = wrapper($"{head}<{prefix}sheetData>{rows}</{prefix}sheetData>");
        return Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + rebuilt);
    }

    private string Row(string row, string prefix, Func<string, string> wrapper)
    {
        if (Parses(wrapper(row))) return row;
        if (RowTag.Match(row) is not { Success: true } tag || !Parses(wrapper(tag.Value + $"</{prefix}row>"))) return string.Empty;

        var cells = new StringBuilder();
        foreach (var cell in Chunks(row[tag.Length..], CellStart, $"</{prefix}c>"))
            if (Parses(wrapper(cell))) cells.Append(cell);
        return $"{tag.Value}{cells}</{prefix}row>";
    }

    private IEnumerable<string> Chunks(string text, Regex start, string end)
    {
        var starts = start.Matches(text).Select(match => match.Index).ToList();
        for (var i = 0; i < starts.Count; i++)
        {
            var limit = i + 1 < starts.Count ? starts[i + 1] : text.Length;
            var close = text.IndexOf(end, starts[i], limit - starts[i], StringComparison.Ordinal);
            var tagEnd = text.IndexOf('>', starts[i], limit - starts[i]);
            if (tagEnd > 0 && text[tagEnd - 1] == '/' && (close < 0 || close > tagEnd)) yield return text[starts[i]..(tagEnd + 1)];
            else if (close >= 0) yield return text[starts[i]..(close + end.Length)];
            else yield return text[starts[i]..limit];
        }
    }

    private bool Parses(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), _settings);
            while (reader.Read())
            {
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
