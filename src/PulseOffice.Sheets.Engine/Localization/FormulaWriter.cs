using System.Text;
using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Localization;

public sealed class FormulaWriter(IFunctionNameVocabulary vocabulary, IFormulaPunctuationConvention punctuation)
{
    private readonly BinaryOperators _operators = new();

    public FormulaWriter(IFunctionNameVocabulary vocabulary) : this(vocabulary, new FormulaPunctuationConvention()) { }

    public string Write(FormulaNode node, string language)
    {
        ArgumentNullException.ThrowIfNull(node);

        var builder = new StringBuilder("=");
        node.Accept(new LocalizedFormulaPrinter(builder, _operators, punctuation.For(language), name => vocabulary.ToLanguage(name, language)));
        return builder.ToString();
    }
}
