using PulseOffice.Localization;

namespace PulseOffice.Sheets.UI;

public sealed record FunctionCategoryEntry(string? Category, string Label, string Icon, int Count);

public interface IFunctionCategoryLooks
{
    IReadOnlyList<FunctionCategoryEntry> Entries(IEnumerable<string> categories, Func<string?, int> count);
}

public sealed class FunctionCategoryLooks(ITextCatalog texts) : IFunctionCategoryLooks
{
    private readonly IReadOnlyDictionary<string, string> _keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Compatibilità"] = "compatibility",
        ["Data e ora"] = "dateTime",
        ["Database"] = "database",
        ["Finanza"] = "financial",
        ["Informazione"] = "information",
        ["Ingegneria"] = "engineering",
        ["Integrazione"] = "addIn",
        ["Logica"] = "logical",
        ["Matematica"] = "math",
        ["Matrice"] = "array",
        ["Ricerca e riferimento"] = "lookup",
        ["Statistica"] = "statistical",
        ["Testo"] = "text",
        ["Varie"] = "other",
    };

    public IReadOnlyList<FunctionCategoryEntry> Entries(IEnumerable<string> categories, Func<string?, int> count)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(count);
        var named = categories
            .Select(category => (Category: category, Key: _keys.GetValueOrDefault(category, "other")))
            .Select(entry => new FunctionCategoryEntry(entry.Category,
                _keys.ContainsKey(entry.Category) ? texts["functions.category." + entry.Key] : entry.Category,
                "functions.category." + entry.Key, count(entry.Category)))
            .OrderBy(entry => entry.Label, StringComparer.CurrentCulture);
        return [new FunctionCategoryEntry(null, texts["functions.allCategories"], "functions.category.all", count(null)), .. named];
    }
}
