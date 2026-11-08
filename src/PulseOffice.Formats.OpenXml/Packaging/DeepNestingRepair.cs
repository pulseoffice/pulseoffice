using System.Xml;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DeepNestingRepair : IPackageRepairStep
{
    public const int MaxDepth = 384;

    private readonly XmlReaderSettings _reading = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false };

    public bool Apply(PackageModel package)
    {
        var changed = false;
        foreach (var name in package.Names.Where(IsXml).ToList())
        {
            var bytes = package[name]!;
            if (!TooDeep(bytes)) continue;
            package[name] = Trimmed(bytes);
            package.Damage.Add(new PartDamage(name, PartDamageKind.Trimmed));
            changed = true;
        }

        return changed;
    }

    private bool IsXml(string name) =>
        name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".vml", StringComparison.OrdinalIgnoreCase);

    private bool TooDeep(byte[] bytes)
    {
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(bytes), _reading);
            while (reader.Read())
                if (reader.NodeType == XmlNodeType.Element && reader.Depth > MaxDepth) return true;
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private byte[] Trimmed(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var reader = XmlReader.Create(new MemoryStream(bytes), _reading))
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false) }))
        {
            reader.Read();
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Depth > MaxDepth)
                {
                    reader.Skip();
                    continue;
                }
                Copy(reader, writer);
                reader.Read();
            }
        }

        return output.ToArray();
    }

    private void Copy(XmlReader reader, XmlWriter writer)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                var empty = reader.IsEmptyElement;
                writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                writer.WriteAttributes(reader, true);
                if (empty) writer.WriteEndElement();
                break;
            case XmlNodeType.EndElement:
                writer.WriteFullEndElement();
                break;
            case XmlNodeType.Text:
                writer.WriteString(reader.Value);
                break;
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                writer.WriteWhitespace(reader.Value);
                break;
            case XmlNodeType.CDATA:
                writer.WriteCData(reader.Value);
                break;
            case XmlNodeType.ProcessingInstruction:
                writer.WriteProcessingInstruction(reader.Name, reader.Value);
                break;
            case XmlNodeType.XmlDeclaration:
                writer.WriteStartDocument();
                break;
        }
    }
}
