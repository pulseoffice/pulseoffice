namespace PulseOffice.Text.Rich;

public interface IVerticalForms
{
    string Substitute(string text);
}

public sealed class VerticalForms : IVerticalForms
{
    private readonly Dictionary<char, char> _forms = new()
    {
        ['，'] = '︐',
        ['、'] = '︑',
        ['。'] = '︒',
        ['：'] = '︓',
        ['；'] = '︔',
        ['！'] = '︕',
        ['？'] = '︖',
        ['〖'] = '︗',
        ['〗'] = '︘',
        ['＿'] = '︳',
        ['（'] = '︵',
        ['）'] = '︶',
        ['｛'] = '︷',
        ['｝'] = '︸',
        ['〔'] = '︹',
        ['〕'] = '︺',
        ['【'] = '︻',
        ['】'] = '︼',
        ['《'] = '︽',
        ['》'] = '︾',
        ['〈'] = '︿',
        ['〉'] = '﹀',
        ['「'] = '﹁',
        ['」'] = '﹂',
        ['『'] = '﹃',
        ['』'] = '﹄',
        ['［'] = '﹇',
        ['］'] = '﹈',
    };

    public string Substitute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Any(_forms.ContainsKey)) return text;
        return string.Create(text.Length, text, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
                span[i] = _forms.TryGetValue(source[i], out var form) ? form : source[i];
        });
    }
}
