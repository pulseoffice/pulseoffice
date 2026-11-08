using PulseOffice.Foundation.Modules;

namespace PulseOffice.Scripting.Editor;

public sealed record DocumentationParameter(string Name, string Description);

public sealed record DocumentationTopic(
    string Title,
    string Signature,
    string Summary,
    IReadOnlyList<DocumentationParameter> Parameters,
    string Insert,
    string Group);

public interface IScriptDocumentation
{
    IReadOnlyList<DocumentationTopic> Topics(ModuleKind module);

    IReadOnlyList<DocumentationTopic> Search(ModuleKind module, string query);
}

public sealed class ScriptDocumentation(IScriptApiProvider api) : IScriptDocumentation
{
    public IReadOnlyList<DocumentationTopic> Topics(ModuleKind module)
    {
        var topics = new List<DocumentationTopic>();

        foreach (var root in api.Catalog.RootsFor(module).Where(symbol => !symbol.IsAlias))
        {
            topics.Add(new DocumentationTopic(
                root.Name,
                root.Signature ?? root.TypeName ?? root.Name,
                root.Documentation ?? string.Empty,
                [],
                root.Name,
                root.Name));

            foreach (var member in root.Members)
                topics.Add(new DocumentationTopic(
                    $"{root.Name}.{member.Name}",
                    member.Signature ?? member.Name,
                    member.Documentation ?? string.Empty,
                    [.. member.Parameters.Select(parameter =>
                        new DocumentationParameter(parameter.Name, parameter.Documentation ?? string.Empty))],
                    member.Kind == ApiSymbolKind.Method
                        ? $"{root.Name}.{member.Name}("
                        : $"{root.Name}.{member.Name}",
                    root.Name));
        }

        return topics;
    }

    public IReadOnlyList<DocumentationTopic> Search(ModuleKind module, string query)
    {
        var topics = Topics(module);

        if (string.IsNullOrWhiteSpace(query)) return topics;

        var needle = query.Trim();

        return [.. topics.Where(topic =>
            topic.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
            || topic.Summary.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
            || topic.Parameters.Any(parameter =>
                parameter.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
                || parameter.Description.Contains(needle, StringComparison.CurrentCultureIgnoreCase)))];
    }
}
