using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Robustness.Tests.Fuzzing;

public interface IFileMutation
{
    string Name { get; }

    byte[] Apply(byte[] original, Random random);
}

public sealed class ByteFlip : IFileMutation
{
    public string Name => "byte-flip";

    public byte[] Apply(byte[] original, Random random)
    {
        var copy = (byte[])original.Clone();
        if (copy.Length == 0) return copy;
        for (var flips = random.Next(1, 9); flips > 0; flips--)
            copy[random.Next(copy.Length)] ^= (byte)(1 << random.Next(8));
        return copy;
    }
}

public sealed class Truncation : IFileMutation
{
    public string Name => "truncation";

    public byte[] Apply(byte[] original, Random random) => original[..random.Next(Math.Max(1, original.Length))];
}

public sealed class TailCorruption : IFileMutation
{
    public string Name => "tail-corruption";

    public byte[] Apply(byte[] original, Random random)
    {
        var copy = (byte[])original.Clone();
        var start = Math.Max(0, copy.Length - 64);
        for (var index = start; index < copy.Length; index++)
            if (random.Next(3) == 0) copy[index] = (byte)random.Next(256);
        return copy;
    }
}

public abstract class ZipEntryMutation : IFileMutation
{
    public abstract string Name { get; }

    public byte[] Apply(byte[] original, Random random)
    {
        if (!IsZip(original)) return MutateText(original, random);

        try
        {
            using var source = new ZipArchive(new MemoryStream(original), ZipArchiveMode.Read);
            var entries = source.Entries.Where(entry => !entry.FullName.EndsWith('/')).ToList();
            var candidates = entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 0) return original;
            var target = Pick(candidates, random);

            using var output = new MemoryStream();
            using (var destination = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in entries)
                {
                    using var read = entry.Open();
                    using var buffer = new MemoryStream();
                    read.CopyTo(buffer);
                    var content = buffer.ToArray();
                    if (entry == target)
                    {
                        var mutated = MutateEntry(entry.FullName, content, random);
                        if (mutated is null) continue;
                        content = mutated;
                    }

                    var written = destination.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
                    using var write = written.Open();
                    write.Write(content);
                }
            }

            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return original;
        }
    }

    protected virtual ZipArchiveEntry Pick(IReadOnlyList<ZipArchiveEntry> candidates, Random random)
    {
        var main = candidates.Where(entry => entry.FullName is "content.xml" or "styles.xml" || entry.FullName.Contains("document", StringComparison.Ordinal) || entry.FullName.Contains("sheet", StringComparison.Ordinal) || entry.FullName.Contains("slide", StringComparison.Ordinal)).ToList();
        var pool = main.Count > 0 && random.Next(3) > 0 ? main : candidates;
        return pool[random.Next(pool.Count)];
    }

    protected virtual byte[]? MutateEntry(string name, byte[] content, Random random) =>
        Encoding.UTF8.GetBytes(MutateXml(Encoding.UTF8.GetString(content), random));

    protected virtual byte[] MutateText(byte[] original, Random random) =>
        Encoding.UTF8.GetBytes(MutateXml(Encoding.UTF8.GetString(original), random));

    protected abstract string MutateXml(string xml, Random random);

    private static bool IsZip(byte[] data) => data.Length > 4 && data[0] == 'P' && data[1] == 'K';
}

public sealed class EntryRemoval : ZipEntryMutation
{
    public override string Name => "entry-removal";

    protected override ZipArchiveEntry Pick(IReadOnlyList<ZipArchiveEntry> candidates, Random random) => candidates[random.Next(candidates.Count)];

    protected override byte[]? MutateEntry(string name, byte[] content, Random random) => null;

    protected override byte[] MutateText(byte[] original, Random random) => original;

    protected override string MutateXml(string xml, Random random) => xml;
}

public sealed class MalformedXml : ZipEntryMutation
{
    public override string Name => "malformed-xml";

    protected override string MutateXml(string xml, Random random)
    {
        var tags = Regex.Matches(xml, "</?[A-Za-z][^>]*>");
        if (tags.Count == 0) return xml;
        var tag = tags[random.Next(tags.Count)];
        return random.Next(3) switch
        {
            0 => xml.Remove(tag.Index, tag.Length),
            1 => xml.Insert(tag.Index + 1, "&bogus;<"),
            _ => xml[..(tag.Index + tag.Length / 2)],
        };
    }
}

public sealed class OutOfRangeNumbers : ZipEntryMutation
{
    private static readonly string[] Replacements =
    [
        "-1", "0", "-2147483649", "2147483648", "99999999999999999999999", "1e308", "-1e308", "NaN", "Infinity", "-0",
        "9999999in", "-5cm", "1e400pt", "", "999999%", "-999999%", "0x7fffffff",
    ];

    public override string Name => "out-of-range-numbers";

    protected override string MutateXml(string xml, Random random)
    {
        var numbers = Regex.Matches(xml, "=\"(-?[0-9][0-9.]*[a-z%]*)\"");
        if (numbers.Count == 0) return xml;
        var builder = new StringBuilder(xml);
        var picks = Enumerable.Range(0, Math.Min(numbers.Count, random.Next(1, 40))).Select(_ => numbers[random.Next(numbers.Count)]).DistinctBy(match => match.Index).OrderByDescending(match => match.Index);
        foreach (var number in picks)
        {
            var group = number.Groups[1];
            builder.Remove(group.Index, group.Length).Insert(group.Index, Replacements[random.Next(Replacements.Length)]);
        }

        return builder.ToString();
    }
}

public sealed class DuplicateIdentifiers : ZipEntryMutation
{
    public override string Name => "duplicate-ids";

    protected override string MutateXml(string xml, Random random)
    {
        var pattern = new Regex("((?:xml:id|draw:id|text:name|table:name|style:name|w:styleId|w:id|r:id|Id|id|sheetId|name)=\")([^\"]*)\"");
        var first = pattern.Match(xml);
        if (!first.Success) return xml;
        var value = first.Groups[2].Value;
        return pattern.Replace(xml, match => random.Next(2) == 0 ? match.Groups[1].Value + value + "\"" : match.Value);
    }
}

public sealed class CircularReferences : ZipEntryMutation
{
    public override string Name => "circular-references";

    protected override string MutateXml(string xml, Random random)
    {
        var odf = Regex.Replace(xml, "style:name=\"([^\"]+)\"([^>]*?)style:parent-style-name=\"[^\"]+\"", match => $"style:name=\"{match.Groups[1].Value}\"{match.Groups[2].Value}style:parent-style-name=\"{match.Groups[1].Value}\"");
        odf = Regex.Replace(odf, "style:name=\"([^\"]+)\"([^>]*?)style:next-style-name=\"[^\"]+\"", match => $"style:name=\"{match.Groups[1].Value}\"{match.Groups[2].Value}style:next-style-name=\"{match.Groups[1].Value}\"");
        var ooxml = Regex.Replace(odf, "w:styleId=\"([^\"]+)\"(.*?)<w:basedOn w:val=\"[^\"]+\"", match => $"w:styleId=\"{match.Groups[1].Value}\"{match.Groups[2].Value}<w:basedOn w:val=\"{match.Groups[1].Value}\"", RegexOptions.Singleline);
        ooxml = Regex.Replace(ooxml, "<f>[^<]*</f>", "<f>A1+B1</f>");
        ooxml = Regex.Replace(ooxml, "table:formula=\"[^\"]*\"", "table:formula=\"of:=[.A1]+[.B1]\"");
        return ooxml;
    }
}

public sealed class DeepNesting : ZipEntryMutation
{
    private readonly int _depth;

    public DeepNesting(int depth) => _depth = depth;

    public override string Name => $"deep-nesting-{_depth}";

    protected override byte[] MutateText(byte[] original, Random random)
    {
        var text = Encoding.UTF8.GetString(original);
        if (text.TrimStart().StartsWith("{\\rtf", StringComparison.Ordinal))
        {
            var at = text.IndexOf('{', 1);
            if (at < 0) return original;
            return Encoding.UTF8.GetBytes(text.Insert(at, string.Concat(Enumerable.Repeat("{\\b ", _depth)) + "x" + new string('}', _depth)));
        }

        var body = text.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (body >= 0 && random.Next(2) == 0)
        {
            var at = text.IndexOf('>', body) + 1;
            return Encoding.UTF8.GetBytes(text.Insert(at, "&nbsp;" + string.Concat(Enumerable.Repeat("<div><ul><li><table><tr><td><b>", _depth / 7))));
        }

        return base.MutateText(original, random);
    }

    protected override string MutateXml(string xml, Random random)
    {
        var opens = Regex.Matches(xml, "<([A-Za-z][A-Za-z0-9_.:-]*)(\\s[^>]*)?(?<!/)>").Where(match => !match.Value.StartsWith("<?", StringComparison.Ordinal)).ToList();
        if (opens.Count == 0) return xml;
        var chosen = opens[opens.Count / 2 + random.Next(Math.Max(1, opens.Count / 2))];
        var name = chosen.Groups[1].Value;
        var open = $"<{name}>";
        var nested = string.Concat(Enumerable.Repeat(open, _depth)) + string.Concat(Enumerable.Repeat($"</{name}>", _depth));
        return xml.Insert(chosen.Index + chosen.Length, nested);
    }
}
