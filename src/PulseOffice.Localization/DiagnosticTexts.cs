using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Localization;

public interface IDiagnosticTexts
{
    string Describe(Diagnostic diagnostic);
}

public sealed class DiagnosticTexts(ITextCatalog texts) : IDiagnosticTexts
{
    public string Describe(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var key = diagnostic.MessageKey;
        if (!texts.Contains(key)) return diagnostic.Message;

        try
        {
            return texts.Format(key, [.. diagnostic.Arguments]);
        }
        catch (FormatException)
        {
            return diagnostic.Message;
        }
    }
}
