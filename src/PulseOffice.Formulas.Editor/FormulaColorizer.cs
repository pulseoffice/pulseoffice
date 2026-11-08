using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using PulseOffice.Formulas;

namespace PulseOffice.Formulas.Editor;

public sealed class FormulaColorizer(IFormulaVocabulary vocabulary, Func<string, IBrush> brushes)
    : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        var index = 0;

        while (index < text.Length)
        {
            var character = text[index];

            if (character == '"')
            {
                var close = text.IndexOf('"', index + 1);
                var end = close < 0 ? text.Length : close + 1;

                Paint(line, index, end, "text");
                index = end;
                continue;
            }

            if (char.IsLetter(character))
            {
                var start = index;
                while (index < text.Length && char.IsLetter(text[index])) index++;

                Paint(line, start, index, RoleOf(text[start..index]));
                continue;
            }

            if (char.IsDigit(character))
            {
                var start = index;
                while (index < text.Length && (char.IsDigit(text[index]) || text[index] is '.' or ',')) index++;

                Paint(line, start, index, "number");
                continue;
            }

            if (character is '^' or '_' or '&' or '#') Paint(line, index, index + 1, "script");
            else if (character is '{' or '}' or '(' or ')' or '[' or ']') Paint(line, index, index + 1, "fence");

            index++;
        }
    }

    private string RoleOf(string word) =>
        vocabulary.Constructs.Contains(word) ? "construct"
        : vocabulary.LargeOperators.ContainsKey(word) ? "large"
        : vocabulary.Functions.Contains(word) ? "function"
        : vocabulary.Symbols.ContainsKey(word) ? "symbol"
        : "identifier";

    private void Paint(DocumentLine line, int start, int end, string role)
    {
        if (role == "identifier") return;

        ChangeLinePart(line.Offset + start, line.Offset + end, element =>
        {
            element.TextRunProperties.SetForegroundBrush(brushes(role));

            if (role is "construct" or "large")
                element.TextRunProperties.SetTypeface(new Typeface(
                    element.TextRunProperties.Typeface.FontFamily, FontStyle.Normal, FontWeight.SemiBold));
        });
    }
}
