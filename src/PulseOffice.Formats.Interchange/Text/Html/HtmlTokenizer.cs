using System.Net;
using System.Text;

namespace PulseOffice.Formats.Interchange.Text.Html;

public abstract record HtmlToken;

public sealed record HtmlText(string Text) : HtmlToken;

public sealed record HtmlStartTag(string Name, IReadOnlyDictionary<string, string> Attributes, bool SelfClosing) : HtmlToken
{
    public string? Attribute(string name) => Attributes.TryGetValue(name, out var value) ? value : null;
}

public sealed record HtmlEndTag(string Name) : HtmlToken;

public interface IHtmlTokenizer
{
    IEnumerable<HtmlToken> Tokenize(string html);
}

public sealed class HtmlTokenizer : IHtmlTokenizer
{
    private const int MaximumAttributes = 64;

    private readonly HashSet<string> _rawText = new(StringComparer.Ordinal) { "script", "style", "textarea", "title", "xmp", "noscript", "template", "iframe", "noembed", "noframes" };

    public IEnumerable<HtmlToken> Tokenize(string html)
    {
        var position = 0;
        var text = new StringBuilder();
        while (position < html.Length)
        {
            var open = html.IndexOf('<', position);
            if (open < 0)
            {
                text.Append(html, position, html.Length - position);
                break;
            }

            text.Append(html, position, open - position);
            if (html.AsSpan(open).StartsWith("<!--"))
            {
                var close = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
                position = close < 0 ? html.Length : close + 3;
                continue;
            }

            if (open + 1 < html.Length && (html[open + 1] is '!' or '?'))
            {
                var close = html.IndexOf('>', open);
                position = close < 0 ? html.Length : close + 1;
                continue;
            }

            if (open + 1 >= html.Length || !(char.IsAsciiLetter(html[open + 1]) || html[open + 1] == '/'))
            {
                text.Append('<');
                position = open + 1;
                continue;
            }

            if (text.Length > 0)
            {
                yield return new HtmlText(WebUtility.HtmlDecode(text.ToString()));
                text.Clear();
            }

            var token = Tag(html, open, out position);
            if (token is null) continue;
            yield return token;
            if (token is HtmlStartTag { SelfClosing: false } start && _rawText.Contains(start.Name))
            {
                var end = html.IndexOf("</" + start.Name, position, StringComparison.OrdinalIgnoreCase);
                var content = end < 0 ? html[position..] : html[position..end];
                if (start.Name == "title") yield return new HtmlText(WebUtility.HtmlDecode(content));
                if (end < 0)
                {
                    position = html.Length;
                    yield return new HtmlEndTag(start.Name);
                    break;
                }
                position = end;
            }
        }

        if (text.Length > 0) yield return new HtmlText(WebUtility.HtmlDecode(text.ToString()));
    }

    private HtmlToken? Tag(string html, int open, out int position)
    {
        var index = open + 1;
        var closing = html[index] == '/';
        if (closing) index++;
        var nameStart = index;
        while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] is not ('>' or '/')) index++;
        var name = html[nameStart..index].ToLowerInvariant();
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var selfClosing = false;
        while (index < html.Length && html[index] != '>')
        {
            if (char.IsWhiteSpace(html[index]))
            {
                index++;
                continue;
            }
            if (html[index] == '/')
            {
                selfClosing = true;
                index++;
                continue;
            }

            var attributeStart = index;
            while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] is not ('=' or '>' or '/')) index++;
            var attribute = html[attributeStart..index].ToLowerInvariant();
            while (index < html.Length && char.IsWhiteSpace(html[index])) index++;
            var value = string.Empty;
            if (index < html.Length && html[index] == '=')
            {
                index++;
                while (index < html.Length && char.IsWhiteSpace(html[index])) index++;
                if (index < html.Length && html[index] is '"' or '\'')
                {
                    var quote = html[index];
                    var end = html.IndexOf(quote, index + 1);
                    if (end < 0) end = html.Length;
                    value = html[(index + 1)..end];
                    index = Math.Min(html.Length, end + 1);
                }
                else
                {
                    var valueStart = index;
                    while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] != '>') index++;
                    value = html[valueStart..index];
                }
            }

            if (attribute.Length > 0 && attributes.Count < MaximumAttributes) attributes.TryAdd(attribute, WebUtility.HtmlDecode(value));
            if (index == attributeStart) index++;
        }

        position = Math.Min(html.Length, index + 1);
        if (name.Length == 0) return null;
        return closing ? new HtmlEndTag(name) : new HtmlStartTag(name, attributes, selfClosing);
    }
}
