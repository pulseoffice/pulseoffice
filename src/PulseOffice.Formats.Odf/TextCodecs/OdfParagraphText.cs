using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal interface IOdfParagraphText
{
    IEnumerable<XElement> Paragraphs(string text);

    XElement Paragraph(string line);

    string Read(XElement container);

    string Line(XElement paragraph);

    XElement Shallow(XElement paragraph);
}

internal sealed class OdfParagraphText : IOdfParagraphText
{
    private const int DeepestNesting = 64;

    public IEnumerable<XElement> Paragraphs(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(Paragraph);

    public XElement Paragraph(string line)
    {
        var paragraph = new XElement(Namespaces.Text + "p");
        var buffer = new StringBuilder();
        var index = 0;

        while (index < line.Length)
        {
            var c = line[index];
            if (c is '\n' or '\r')
            {
                Flush(paragraph, buffer);
                if (c == '\n') paragraph.Add(new XElement(Namespaces.Text + "line-break"));
                index++;
                continue;
            }

            if (c == '\t')
            {
                Flush(paragraph, buffer);
                paragraph.Add(new XElement(Namespaces.Text + "tab"));
                index++;
                continue;
            }

            if (c == ' ')
            {
                var run = 1;
                while (index + run < line.Length && line[index + run] == ' ') run++;
                var leading = index == 0 || buffer.Length == 0 && paragraph.LastNode is XElement;
                var literal = leading ? 0 : 1;
                buffer.Append(' ', literal);
                if (run > literal)
                {
                    Flush(paragraph, buffer);
                    var spaces = new XElement(Namespaces.Text + "s");
                    if (run - literal > 1) spaces.Add(new XAttribute(Namespaces.Text + "c", (run - literal).ToString(CultureInfo.InvariantCulture)));
                    paragraph.Add(spaces);
                }
                index += run;
                continue;
            }

            buffer.Append(c);
            index++;
        }

        Flush(paragraph, buffer);
        return paragraph;
    }

    public string Read(XElement container) => string.Join('\n', TopParagraphs(container).Select(Line));

    private IEnumerable<XElement> TopParagraphs(XElement container)
    {
        var pending = new Stack<XElement>();
        Push(container);
        while (pending.TryPop(out var element))
        {
            if (element.Name == Namespaces.Text + "p" || element.Name == Namespaces.Text + "h") yield return element;
            else if (element.Name.Namespace != Namespaces.Office) Push(element);
        }

        void Push(XElement parent)
        {
            var children = parent.Elements().ToList();
            for (var position = children.Count - 1; position >= 0; position--) pending.Push(children[position]);
        }
    }

    public string Line(XElement paragraph)
    {
        var text = new CollapsingText();
        Append(paragraph, text);
        return text.ToString();
    }

    public XElement Shallow(XElement paragraph)
    {
        if (!Deeper(paragraph, DeepestNesting)) return paragraph;
        var flat = new XElement(paragraph.Name, paragraph.Attributes());
        flat.Add(Paragraph(Line(paragraph)).Nodes());
        return flat;
    }

    private bool Deeper(XElement root, int limit)
    {
        var pending = new Stack<(XElement Element, int Depth)>();
        pending.Push((root, 0));
        while (pending.TryPop(out var current))
        {
            if (current.Depth > limit) return true;
            foreach (var child in current.Element.Elements()) pending.Push((child, current.Depth + 1));
        }
        return false;
    }

    private void Append(XElement element, CollapsingText text)
    {
        var pending = new Stack<XNode>();
        Push(element);
        while (pending.TryPop(out var node))
        {
            switch (node)
            {
                case XText run:
                    text.AppendCollapsible(run.Value);
                    break;
                case XElement child when child.Name == Namespaces.Text + "s":
                    var count = int.TryParse((string?)child.Attribute(Namespaces.Text + "c"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) ? Math.Clamp(c, 1, 100_000) : 1;
                    text.Append(' ', count);
                    break;
                case XElement child when child.Name == Namespaces.Text + "tab":
                    text.Append('\t');
                    break;
                case XElement child when child.Name == Namespaces.Text + "line-break":
                    text.Append('\n');
                    break;
                case XElement child when child.Name.Namespace == Namespaces.Office:
                    break;
                case XElement child:
                    Push(child);
                    break;
            }
        }

        void Push(XElement parent)
        {
            var children = parent.Nodes().ToList();
            for (var position = children.Count - 1; position >= 0; position--) pending.Push(children[position]);
        }
    }

    private sealed class CollapsingText
    {
        private readonly StringBuilder _text = new();
        private bool _collapsible;

        public void AppendCollapsible(string value)
        {
            foreach (var c in value)
            {
                if (c is ' ' or '\t' or '\r' or '\n')
                {
                    if (_collapsible) continue;
                    _text.Append(' ');
                    _collapsible = true;
                    continue;
                }

                _text.Append(c);
                _collapsible = false;
            }
        }

        public void Append(char c, int count = 1)
        {
            _text.Append(c, count);
            _collapsible = false;
        }

        public override string ToString() => _text.ToString();
    }

    private void Flush(XElement paragraph, StringBuilder buffer)
    {
        if (buffer.Length == 0) return;
        paragraph.Add(new XText(buffer.ToString()));
        buffer.Clear();
    }
}
