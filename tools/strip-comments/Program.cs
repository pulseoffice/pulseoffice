using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

var roots = args.Length > 0 ? args : ["src", "tests"];
var changed = 0;

foreach (var root in roots)
{
    foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
    {
        if (file.Contains("/obj/", StringComparison.Ordinal)) continue;
        if (file.Contains("/bin/", StringComparison.Ordinal)) continue;

        var original = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(original);
        using var workspace = new AdhocWorkspace();

        var stripped = new CommentRemover().Visit(tree.GetRoot());
        var rewritten = Tidy(Formatter.Format(stripped, workspace).ToFullString());
        if (rewritten == original) continue;

        File.WriteAllText(file, rewritten);
        changed++;
    }
}

Console.WriteLine($"{changed} files rewritten");

static string Tidy(string text)
{
    var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    var result = new List<string>(lines.Length);
    var blanks = 0;

    foreach (var line in lines)
    {
        var trimmed = line.TrimEnd();

        if (trimmed.Length == 0)
        {
            blanks++;
            if (blanks > 1) continue;
        }
        else
        {
            blanks = 0;
        }

        result.Add(trimmed);
    }

    while (result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);

    var joined = string.Join("\n", result);

    while (joined.Contains("{\n\n", StringComparison.Ordinal))
        joined = joined.Replace("{\n\n", "{\n", StringComparison.Ordinal);

    while (joined.Contains("\n\n}", StringComparison.Ordinal))
        joined = joined.Replace("\n\n}", "\n}", StringComparison.Ordinal);

    return joined + "\n";
}

sealed class CommentRemover : CSharpSyntaxRewriter
{
    public CommentRemover() : base(visitIntoStructuredTrivia: true) { }

    public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia)
            ? default
            : base.VisitTrivia(trivia);
}
