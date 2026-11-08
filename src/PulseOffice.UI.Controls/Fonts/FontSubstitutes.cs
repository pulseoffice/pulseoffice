namespace PulseOffice.UI.Controls.Fonts;

public interface IFontSubstitutes
{
    IReadOnlyList<string> For(string family);
}

public sealed class MetricCompatibleSubstitutes : IFontSubstitutes
{
    private readonly Dictionary<string, string[]> _table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Liberation Serif"] = ["Tinos", "Times New Roman", "Times", "Noto Serif", "DejaVu Serif"],
        ["Liberation Sans"] = ["Arimo", "Arial", "Helvetica", "Noto Sans", "DejaVu Sans"],
        ["Liberation Mono"] = ["Cousine", "Courier New", "Courier", "Noto Sans Mono", "DejaVu Sans Mono"],
        ["Times New Roman"] = ["Liberation Serif", "Tinos", "Times"],
        ["Arial"] = ["Liberation Sans", "Arimo", "Helvetica"],
        ["Courier New"] = ["Liberation Mono", "Cousine", "Courier"],
        ["Carlito"] = ["Calibri"],
        ["Calibri"] = ["Carlito"],
        ["Caladea"] = ["Cambria"],
        ["Cambria"] = ["Caladea"],
    };

    public IReadOnlyList<string> For(string family) => _table.TryGetValue(family, out var list) ? list : [];
}
