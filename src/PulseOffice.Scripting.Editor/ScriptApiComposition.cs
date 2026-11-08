using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;

namespace PulseOffice.Scripting.Editor;

public sealed record ApiMemberSpec(string Member, string Type, string Signature, string[] Parameters, bool OutsideHistory = false);

public sealed record ApiRootSpec(ModuleKind Module, string Name, string Type, IReadOnlyList<ApiMemberSpec> Members);

public interface IScriptApiSection
{
    IEnumerable<ApiRootSpec> Roots { get; }
}

public interface IScriptApiProvider
{
    ScriptApiCatalog Catalog { get; }
}

public sealed class ScriptApiProvider : IScriptApiProvider
{
    private readonly IEnumerable<IScriptApiSection> _sections;
    private readonly ITextCatalog _texts;
    private readonly Lock _gate = new();
    private ScriptApiCatalog? _catalog;

    public ScriptApiProvider(IEnumerable<IScriptApiSection> sections, ITextCatalog texts)
    {
        _sections = sections;
        _texts = texts;
        _texts.LanguageChanged += (_, _) =>
        {
            lock (_gate) _catalog = null;
        };
    }

    public ScriptApiCatalog Catalog
    {
        get
        {
            lock (_gate) return _catalog ??= Build();
        }
    }

    private ScriptApiCatalog Build()
    {
        var catalog = new ScriptApiCatalog();
        var factory = new ApiRootFactory(_texts);
        foreach (var root in _sections.SelectMany(section => section.Roots))
            catalog.Register(factory.Create(root));
        return catalog;
    }
}

public sealed class ApiRootFactory(ITextCatalog texts)
{
    public ApiSymbol Create(ApiRootSpec root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var type = root.Type.ToLowerInvariant();

        return new ApiSymbol
        {
            Name = root.Name,
            Kind = ApiSymbolKind.Class,
            QualifiedName = root.Name,
            Module = root.Module,
            TypeName = root.Type,
            Documentation = texts[$"api.{type}.summary"],
            Title = texts[$"api.{type}.readProperties.title"],
            Members = [.. root.Members.Select(member => Member(root, type, member))],
        };
    }

    private ApiSymbol Member(ApiRootSpec root, string type, ApiMemberSpec member) => new()
    {
        Name = member.Member,
        Kind = member.Signature.Contains('(') ? ApiSymbolKind.Method : ApiSymbolKind.Property,
        QualifiedName = $"{root.Name}.{member.Member}",
        Module = root.Module,
        TypeName = member.Type,
        Signature = member.Signature + ": " + member.Type,
        Documentation = texts[$"api.{type}.{member.Member}.summary"],
        Title = texts[$"api.{type}.{member.Member}.title"],
        Parameters = [.. member.Parameters.Select(Parameter)],
        IsOutsideHistory = member.OutsideHistory,
    };

    private ApiParameter Parameter(string parameter)
    {
        var parts = parameter.Split(':', 2);
        return new ApiParameter(parts[0], string.Empty, IsOptional: false, texts[$"api.param.{parts[1]}"]);
    }
}

public sealed class ShellScriptApi : IScriptApiSection
{
    public IEnumerable<ApiRootSpec> Roots { get; } =
    [
        new(ModuleKind.Shell, "app", "Application",
        [
            new("Name", "string", "Name: string", []),
            new("Module", "string", "Module: string", []),
            new("Version", "string", "Version: string", []),
            new("Language", "string", "Language: string", []),
            new("Log", "void", "Log(message)", ["message:any"]),
            new("Status", "void", "Status(message)", ["message:any"]),
            new("HasClipboard", "boolean", "HasClipboard: boolean", []),
            new("Copy", "void", "Copy()", []),
            new("Cut", "void", "Cut()", []),
            new("Paste", "void", "Paste()", []),
            new("PasteValues", "void", "PasteValues()", []),
            new("Run", "boolean", "Run(command)", ["command:commandId"]),
        ]),
        new(ModuleKind.Shell, "files", "Files",
        [
            new("Folder", "string", "Folder: string", []),
            new("WriteText", "string", "WriteText(name, text)", ["name:fileName", "text:fileText"]),
            new("AppendText", "string", "AppendText(name, text)", ["name:fileName", "text:fileText"]),
            new("Exists", "boolean", "Exists(name)", ["name:fileName"]),
        ]),
    ];
}
