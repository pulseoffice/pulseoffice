using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class DesignRulesTests
{
    private const int LineCeiling = 800;

    private static readonly Lazy<IReadOnlyList<(string Project, string Path, SyntaxNode Root)>> Parsed = new(ParseAll);

    private readonly IReadOnlyDictionary<string, string> oversizedTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PulseOffice.Sheets.Engine:FunctionNameVocabulary"] = "tabella dei nomi delle funzioni nelle cinque lingue: dato, non logica",
        ["PulseOffice.Desktop:PulseMenuLayout"] = "dichiarazione dei menu: dato",
        ["PulseOffice.Documents.Layout:Paginator"] = "da dividere: impaginazione, note e colonne nello stesso ciclo (revisione 2026-10, resta)",
        ["PulseOffice.Presentations.UI:SlideView"] = "da dividere: gesti degli oggetti da condividere con Ledger e Folio (revisione 2026-10, resta)",
        ["PulseOffice.Documents.UI:FolioCanvas"] = "da dividere: gesti degli oggetti da condividere con Ledger e Deck (revisione 2026-10, resta)",
        ["PulseOffice.Sheets.Engine:CalculationEngine"] = "da dividere: grafo delle dipendenze e ricalcolo (revisione 2026-10, resta)",
        ["PulseOffice.Formats.Interchange:RtfReadSession"] = "da dividere per gruppi di parole di controllo (revisione 2026-10, resta)",
        ["PulseOffice.Documents.UI:FolioEditor"] = "facciata dell'editor di Folio; i comandi che lo aggirano vanno riportati qui prima di dividerlo (revisione 2026-10, resta)",
    };

    private readonly IReadOnlyDictionary<string, string> statefulStaticClasses = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PulseOffice.Desktop:Program"] = "punto d'ingresso del processo",
        ["PulseOffice.Diagnostics:LogConfiguration"] = "registrazione avviata prima del contenitore (decisione 14)",
        ["PulseOffice.Desktop:ServiceConfiguration"] = "radice di composizione",
        ["PulseOffice.Desktop:PulseMenuLayout"] = "dichiarazione dei menu",
        ["PulseOffice.Desktop:PulseRibbonLayout"] = "dichiarazione della ribbon",
        ["PulseOffice.Desktop:PulseContextLayout"] = "dichiarazione dei menu contestuali",
    };

    private readonly Regex duplicatedUnit = new("^(Emu|Twips?)Per(Twip|Point|Inch|Centimet(er|re))$", RegexOptions.CultureInvariant);

    [Fact]
    public void Nessun_tipo_supera_il_tetto_di_righe_senza_un_motivo_dichiarato()
    {
        var sizes = Parsed.Value
            .SelectMany(file => file.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(type => type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                .Select(type => (Key: $"{file.Project}:{type.Identifier.ValueText}", Lines: Lines(type))))
            .GroupBy(entry => entry.Key, entry => entry.Lines)
            .ToDictionary(group => group.Key, group => group.Sum());

        var unexplained = sizes.Where(size => size.Value > LineCeiling && !oversizedTypes.ContainsKey(size.Key))
            .Select(size => $"{size.Key} ({size.Value})").ToList();
        var stale = oversizedTypes.Keys.Where(key => !sizes.TryGetValue(key, out var lines) || lines <= LineCeiling).ToList();

        Assert.True(unexplained.Count == 0, "oltre il tetto: " + string.Join("; ", unexplained));
        Assert.True(stale.Count == 0, "eccezioni non più necessarie: " + string.Join("; ", stale));
    }

    [Fact]
    public void Le_classi_statiche_sono_solo_costanti_estensioni_registrazioni_o_calcoli_puri()
    {
        var found = Parsed.Value
            .SelectMany(file => file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(type => type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax && type.Modifiers.Any(SyntaxKind.StaticKeyword))
                .Where(type => !Admissible(type, file.Path) && !statefulStaticClasses.ContainsKey($"{file.Project}:{type.Identifier.ValueText}"))
                .Select(type => $"{file.Project}:{type.Identifier.ValueText}"))
            .ToList();

        Assert.True(found.Count == 0, string.Join("; ", found));
    }

    [Fact]
    public void Le_unita_di_misura_si_prendono_da_Foundation_invece_di_ridichiararle()
    {
        var found = Parsed.Value
            .Where(file => file.Project != "PulseOffice.Foundation")
            .SelectMany(file => file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
                .SelectMany(field => field.Declaration.Variables)
                .Where(variable => duplicatedUnit.IsMatch(variable.Identifier.ValueText))
                .Select(variable => $"{Path.GetFileName(file.Path)}:{variable.Identifier.ValueText}"))
            .ToList();

        Assert.True(found.Count == 0, string.Join("; ", found));
    }

    private static bool Admissible(ClassDeclarationSyntax type, string path)
    {
        var name = type.Identifier.ValueText;
        if (name.EndsWith("ServiceCollectionExtensions", StringComparison.Ordinal) || name.EndsWith("Composition", StringComparison.Ordinal)
            || name.EndsWith("Registration", StringComparison.Ordinal) || name == "AssemblyMarker") return true;
        if (path.Contains($"{Path.DirectorySeparatorChar}Numerics{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) return true;
        var methods = type.Members.OfType<MethodDeclarationSyntax>().ToList();
        return methods.All(method => IsExtension(method) || IsPrivateHelper(method) || IsPure(method));
    }

    private static bool IsExtension(MethodDeclarationSyntax method) =>
        method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true;

    private static bool IsPrivateHelper(MethodDeclarationSyntax method) =>
        !method.Modifiers.Any(SyntaxKind.PublicKeyword) && !method.Modifiers.Any(SyntaxKind.InternalKeyword);

    private static bool IsPure(MethodDeclarationSyntax method) =>
        !method.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>()
            .Any(creation => creation is ObjectCreationExpressionSyntax { Type: var type } && Collaborator().IsMatch(type.ToString()));

    private static Regex Collaborator() =>
        new("(Service|Factory|Engine|Reader|Writer|Mapper|Codec|Exporter|Importer|Renderer|Painter|Registry)$", RegexOptions.CultureInvariant);

    private static int Lines(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    private static IReadOnlyList<(string Project, string Path, SyntaxNode Root)> ParseAll()
    {
        var sources = Sources();
        return [.. sources.EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .AsParallel()
            .Select(file => (
                Path.GetRelativePath(sources.FullName, file.FullName).Split(Path.DirectorySeparatorChar)[0],
                file.FullName,
                CSharpSyntaxTree.ParseText(File.ReadAllText(file.FullName), new CSharpParseOptions(LanguageVersion.Preview)).GetRoot()))];
    }

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
