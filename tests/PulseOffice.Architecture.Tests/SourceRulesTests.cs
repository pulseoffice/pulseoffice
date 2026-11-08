using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class SourceRulesTests
{
    private static readonly Lazy<IReadOnlyList<(string Path, SyntaxNode Root)>> Parsed = new(ParseAll);

    private readonly Regex italian = new(
        "[àèéìòù]|\\b(non(?=\\s)|il|della|delle|degli|nel|nella|per|con|questo|questa|errore|impossibile|salvato|aperto|chiuso|file non)\\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Regex sensitive = new(
        "\\{(Password|Pwd|Secret|Token|ApiKey|Credential|Credentials|ConnectionString|Passphrase|Pin)\\}",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Regex module = new("^PulseOffice\\.(Sheets|Presentations|Documents)(\\.|$)", RegexOptions.CultureInvariant);

    [Fact]
    public void Nessun_commento_nel_codice()
    {
        var found = Parsed.Value
            .SelectMany(file => file.Root.DescendantTrivia()
                .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                .Select(trivia => Where(file.Path, trivia.GetLocation())))
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void Nessun_commento_nel_xaml()
    {
        var found = Sources().EnumerateFiles("*.axaml", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file.FullName) && File.ReadAllText(file.FullName).Contains("<!--", StringComparison.Ordinal))
            .Select(file => file.FullName)
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void Nessuna_compilazione_condizionale()
    {
        var found = Parsed.Value
            .SelectMany(file => file.Root.DescendantTrivia()
                .Where(trivia => trivia.IsKind(SyntaxKind.IfDirectiveTrivia) || trivia.IsKind(SyntaxKind.ElifDirectiveTrivia))
                .Select(trivia => Where(file.Path, trivia.GetLocation())))
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void Nessuno_stato_statico_modificabile()
    {
        var fields = Parsed.Value.SelectMany(file => file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(SyntaxKind.StaticKeyword)
                && !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
                && !field.Modifiers.Any(SyntaxKind.ConstKeyword))
            .Select(field => Where(file.Path, field.GetLocation())));
        var properties = Parsed.Value.SelectMany(file => file.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Modifiers.Any(SyntaxKind.StaticKeyword)
                && property.AccessorList?.Accessors.Any(accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration)) == true)
            .Select(property => Where(file.Path, property.GetLocation())));

        Assert.Empty(fields.Concat(properties).ToList());
    }

    [Fact]
    public void I_servizi_dietro_interfaccia_si_iniettano_invece_di_costruirli_nei_campi()
    {
        var found = Parsed.Value
            .SelectMany(file => file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Where(field => !field.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && ServiceInterface().IsMatch(field.Declaration.Type.ToString())
                    && !field.Declaration.Type.ToString().StartsWith("IReadOnly", StringComparison.Ordinal)
                    && field.Declaration.Type.ToString() is not ("IPen" or "IBrush"))
                .SelectMany(field => field.Declaration.Variables
                    .Where(variable => variable.Initializer?.Value is BaseObjectCreationExpressionSyntax)
                    .Select(variable => $"{Path.GetFileName(file.Path)}:{variable.Identifier.ValueText}")))
            .ToList();

        Assert.True(found.Count == 0, string.Join(";", found));
    }

    [Fact]
    public void Operating_system_checks_live_only_in_the_platform_projects()
    {
        var platform = Path.Combine(Sources().FullName, "PulseOffice.Platform");
        var found = Parsed.Value
            .Where(file => !file.Path.StartsWith(platform, StringComparison.Ordinal))
            .SelectMany(file => file.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .Where(access => access.Expression.ToString() is var target
                    && (target.EndsWith("OperatingSystem", StringComparison.Ordinal) && access.Name.Identifier.ValueText.StartsWith("Is", StringComparison.Ordinal)
                        || target.EndsWith("RuntimeInformation", StringComparison.Ordinal) && access.Name.Identifier.ValueText == "IsOSPlatform"
                        || target.EndsWith("Environment", StringComparison.Ordinal) && access.Name.Identifier.ValueText == "OSVersion"))
                .Select(access => Where(file.Path, access.GetLocation())))
            .ToList();

        Assert.True(found.Count == 0, string.Join(";", found));
    }

    [Fact]
    public void I_messaggi_di_log_sono_in_inglese_e_senza_dati_sensibili()
    {
        var found = new List<string>();
        foreach (var (path, root) in Parsed.Value)
        {
            foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name } || !name.StartsWith("Log", StringComparison.Ordinal)) continue;
                foreach (var literal in call.ArgumentList.Arguments.Select(argument => argument.Expression)
                    .SelectMany(expression => expression.DescendantNodesAndSelf())
                    .OfType<LiteralExpressionSyntax>().Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)))
                {
                    var text = literal.Token.ValueText;
                    if (italian.IsMatch(text) || sensitive.IsMatch(text)) found.Add($"{Where(path, literal.GetLocation())}: {text}");
                }
                foreach (var text in call.ArgumentList.Arguments.SelectMany(argument => argument.Expression.DescendantNodesAndSelf())
                    .OfType<InterpolatedStringTextSyntax>().Select(part => part.TextToken.ValueText))
                    if (italian.IsMatch(text)) found.Add($"{Where(path, call.GetLocation())}: {text}");
            }
        }

        Assert.Empty(found);
    }

    [Fact]
    public void Le_versioni_dei_pacchetti_stanno_solo_nel_file_centrale()
    {
        var root = Sources().Parent!;
        var found = new[] { "src", "tests" }
            .SelectMany(folder => root.GetDirectories(folder).Single().EnumerateFiles("*.csproj", SearchOption.AllDirectories))
            .Where(file => !IsGenerated(file.FullName))
            .SelectMany(file => XDocument.Load(file.FullName).Descendants("PackageReference")
                .Where(element => element.Attribute("Version") is not null || element.Element("Version") is not null)
                .Select(element => $"{file.Name}: {(string?)element.Attribute("Include")}"))
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void I_moduli_non_si_referenziano_fra_loro_e_i_formati_non_toccano_l_interfaccia()
    {
        var found = new List<string>();
        foreach (var project in Sources().GetDirectories("PulseOffice.*"))
        {
            var file = Path.Combine(project.FullName, project.Name + ".csproj");
            if (!File.Exists(file)) continue;
            var references = XDocument.Load(file).Descendants("ProjectReference")
                .Select(element => Path.GetFileNameWithoutExtension(((string?)element.Attribute("Include") ?? "").Replace('\\', '/')))
                .ToList();
            var own = module.Match(project.Name);
            foreach (var reference in references)
            {
                var other = module.Match(reference);
                if (own.Success && other.Success && own.Groups[1].Value != other.Groups[1].Value) found.Add($"{project.Name} -> {reference}");
                if (project.Name.StartsWith("PulseOffice.Formats", StringComparison.Ordinal)
                    && (reference.EndsWith(".UI", StringComparison.Ordinal) || reference.StartsWith("PulseOffice.UI", StringComparison.Ordinal) || reference == "PulseOffice.Shell"))
                    found.Add($"{project.Name} -> {reference}");
            }
        }

        Assert.Empty(found);
    }

    private static Regex ServiceInterface() => new("^(\\w+\\.)*I[A-Z]\\w*$", RegexOptions.CultureInvariant);

    private static string Where(string path, Location location) =>
        $"{Path.GetFileName(path)}:{location.GetLineSpan().StartLinePosition.Line + 1}";

    private static bool IsGenerated(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IReadOnlyList<(string Path, SyntaxNode Root)> ParseAll() =>
        [.. Sources().EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file.FullName))
            .AsParallel()
            .Select(file => (file.FullName, CSharpSyntaxTree.ParseText(File.ReadAllText(file.FullName), new CSharpParseOptions(LanguageVersion.Preview)).GetRoot()))];

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
