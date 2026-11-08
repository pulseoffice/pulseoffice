namespace PulseOffice.UI.Controls.Shapes.Fontwork;

public sealed record FontworkLook(string Key, string Fill, string Line);

public interface IFontworkLooks
{
    IReadOnlyList<FontworkLook> All { get; }
}

public sealed class FontworkLooks : IFontworkLooks
{
    public IReadOnlyList<FontworkLook> All { get; } =
    [
        new("blue", "#2563EB", "#1E3A8A"),
        new("red", "#DC2626", "#7F1D1D"),
        new("gold", "#F59E0B", "#78350F"),
        new("green", "#16A34A", "#14532D"),
        new("grey", "#475569", "#0F172A"),
        new("purple", "#7C3AED", "#3B0764"),
        new("teal", "#0D9488", "#134E4A"),
        new("ink", "#111827", "#111827"),
    ];
}
