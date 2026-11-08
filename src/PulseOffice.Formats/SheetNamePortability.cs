namespace PulseOffice.Formats;

public sealed class SheetNamePortability
{
    private readonly char[] _forbidden = ['[', ']', '*', '?', ':', '/', '\\'];

    public bool AcceptedByOdfSuites(string name) =>
        !string.IsNullOrEmpty(name)
        && !name.StartsWith('\'') && !name.EndsWith('\'')
        && name.IndexOfAny(_forbidden) < 0;

    public bool AcceptedByExcel(string name) =>
        AcceptedByOdfSuites(name) && name.Length <= 31
        && !string.Equals(name, "History", StringComparison.OrdinalIgnoreCase);
}
