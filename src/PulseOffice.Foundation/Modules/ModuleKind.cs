namespace PulseOffice.Foundation.Modules;

public enum ModuleKind
{
    Shell = 0,
    Documents = 1,
    Sheets = 2,
    Presentations = 3,
}

public sealed record ModuleIdentity(
    ModuleKind Kind,
    string BrandName,
    string DescriptionKey,
    string AccentHex,
    string NativeExtension,
    string OpenXmlExtension)
{
    public static readonly ModuleIdentity Documents = new(
        ModuleKind.Documents, "Folio", "module.documents.tagline", "#2563EB", ".odt", ".docx");

    public static readonly ModuleIdentity Sheets = new(
        ModuleKind.Sheets, "Ledger", "module.sheets.tagline", "#16A34A", ".ods", ".xlsx");

    public static readonly ModuleIdentity Presentations = new(
        ModuleKind.Presentations, "Deck", "module.presentations.tagline", "#DC2626", ".odp", ".pptx");

    public static readonly IReadOnlyList<ModuleIdentity> All = [Documents, Sheets, Presentations];

    public static ModuleIdentity For(ModuleKind kind) => kind switch
    {
        ModuleKind.Documents => Documents,
        ModuleKind.Sheets => Sheets,
        ModuleKind.Presentations => Presentations,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The shell has no module identity."),
    };

    public static ModuleIdentity? FromExtension(string extension)
    {
        var e = extension.StartsWith('.') ? extension : '.' + extension;
        return All.FirstOrDefault(m =>
            string.Equals(m.NativeExtension, e, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.OpenXmlExtension, e, StringComparison.OrdinalIgnoreCase));
    }
}
