using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting;

public sealed class PythonLanguage
{
    private readonly CompletionSites _sites = new();

    public IReadOnlySet<string> Keywords { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "and", "as", "assert", "break", "class", "continue", "def", "del", "elif", "else",
        "except", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda",
        "nonlocal", "not", "or", "pass", "raise", "return", "try", "while", "with", "yield",
        "True", "False", "None", "print", "len", "range", "str", "int", "float",
    };

    public IReadOnlyList<CompletionItem> Complete(string source, ScriptPosition position, ScriptApiCatalog api, ModuleKind module)
    {
        ArgumentNullException.ThrowIfNull(api);

        var site = _sites.Find(source, position, ScriptLanguage.Python);

        if (site.Kind == CompletionSiteKind.None) return [];
        if (site.Kind == CompletionSiteKind.Member)
            return [.. api.Complete(site.Path, module).Select(symbol =>
                new CompletionItem(symbol.Name, CompletionKind.Property, symbol.Signature))];

        var items = api.Complete(string.Empty, module)
            .Select(symbol => new CompletionItem(symbol.Name, CompletionKind.Property, symbol.Signature))
            .ToList();

        items.AddRange(Keywords.Select(keyword => new CompletionItem(keyword, CompletionKind.Keyword)));

        return items;
    }
}
