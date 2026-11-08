namespace PulseOffice.Document.Forms;

public enum MacroLanguage
{
    JavaScript,
    Python,
}

public sealed record DocumentMacro(string Name, MacroLanguage Language, string Source);

public interface IMacroDocument
{
    IMacroLibrary Macros { get; }
}

public interface IMacroLibrary
{
    IReadOnlyList<DocumentMacro> All { get; }

    DocumentMacro? Find(string name);

    void Store(DocumentMacro macro);

    bool Remove(string name);

    event EventHandler? Changed;
}

public sealed class MacroLibrary : IMacroLibrary
{
    private readonly Dictionary<string, DocumentMacro> _macros = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<DocumentMacro> All => [.. _macros.Values.OrderBy(macro => macro.Name, StringComparer.CurrentCulture)];

    public event EventHandler? Changed;

    public DocumentMacro? Find(string name) =>
        string.IsNullOrWhiteSpace(name) ? null : _macros.GetValueOrDefault(name.Trim());

    public void Store(DocumentMacro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentException.ThrowIfNullOrWhiteSpace(macro.Name);

        _macros[macro.Name.Trim()] = macro with { Name = macro.Name.Trim() };

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_macros.Remove(name.Trim())) return false;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
