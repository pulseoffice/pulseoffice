using System.Globalization;

namespace PulseOffice.Document.Embedding;

public interface ITableGridLook
{
    string Accent(DataTableStyle style);

    string HeaderFill(DataTableStyle style);

    string BandFill { get; }

    string LineColour { get; }

    double LineWidthPoints { get; }

    string BodyInk { get; }
}

public sealed class TableGridLook : ITableGridLook
{
    private const string DefaultAccent = "#16A34A";
    private const string Slate = "#64748B";
    private const double HeaderTint = 0.14;
    private const double BandTint = 0x14 / 255.0;
    private const double LineTint = 0x40 / 255.0;

    public string Accent(DataTableStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return Parse(style.AccentHex) is not null ? style.AccentHex!.ToUpperInvariant() : DefaultAccent;
    }

    public string HeaderFill(DataTableStyle style) => OverWhite(Accent(style), HeaderTint);

    public string BandFill => OverWhite(Slate, BandTint);

    public string LineColour => OverWhite(Slate, LineTint);

    public double LineWidthPoints => 0.75;

    public string BodyInk => "#1E293B";

    private string OverWhite(string hex, double alpha)
    {
        var rgb = Parse(hex) ?? 0;
        int Channel(int shift) => (int)Math.Round(((rgb >> shift) & 0xFF) * alpha + 255 * (1 - alpha));
        return string.Create(CultureInfo.InvariantCulture, $"#{Channel(16):X2}{Channel(8):X2}{Channel(0):X2}");
    }

    private int? Parse(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb) ? rgb : null;
}
