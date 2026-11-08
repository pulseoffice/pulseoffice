namespace PulseOffice.Document.Fonts;

public sealed record EmbeddedFontFile(string Family, int Weight, bool Italic, string FileName, byte[] Data)
{
    public bool IsBold => Weight >= 600;

    public string StyleName => (IsBold, Italic) switch
    {
        (true, true) => "Bold Italic",
        (true, false) => "Bold",
        (false, true) => "Italic",
        _ => "Regular",
    };
}

public enum FontGenericFamily
{
    Unknown,
    Roman,
    Swiss,
    Modern,
    Script,
    Decorative,
    System,
}

public enum FontPitch
{
    Unknown,
    Variable,
    Fixed,
}

public sealed record FontFaceTraits(string Family, FontGenericFamily Generic, FontPitch Pitch);

public sealed record DocumentFonts(bool EmbedSystemFonts, IReadOnlyList<EmbeddedFontFile> Files)
{
    public IReadOnlyList<FontFaceTraits> Traits { get; init; } = [];

    public FontFaceTraits? TraitsOf(string family) =>
        Traits.FirstOrDefault(traits => string.Equals(traits.Family, family, StringComparison.OrdinalIgnoreCase));

    public static readonly DocumentFonts Empty = new(false, []);

    public bool HasFiles => Files.Count > 0;

    public IReadOnlyList<string> Families =>
        [.. Files.Select(file => file.Family).Distinct(StringComparer.OrdinalIgnoreCase)];

    public IEnumerable<EmbeddedFontFile> FilesOf(string family) =>
        Files.Where(file => string.Equals(file.Family, family, StringComparison.OrdinalIgnoreCase));
}

public interface IFontDocument
{
    DocumentFonts Fonts { get; set; }
}
