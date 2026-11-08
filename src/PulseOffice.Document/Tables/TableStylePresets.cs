namespace PulseOffice.Document.Tables;

public sealed record TableLook(
    string Key,
    string Name,
    uint? HeaderFill,
    uint? HeaderInk,
    uint? BandFill,
    uint? BodyFill,
    uint BorderColor,
    double BorderWidth,
    bool HeaderBold,
    bool InnerLines = true);

public interface ITableStylePresets
{
    IReadOnlyList<TableLook> All { get; }

    TableLook? Find(string key);

    TableLook? Named(string? styleName);

    string Unbanded(TableLook look);
}

public sealed class TableStylePresets : ITableStylePresets
{
    public IReadOnlyList<TableLook> All { get; } =
    [
        new("default", "Default Style", null, null, null, null, 0xFF000000, 0.5, true),
        new("academic", "Academic", null, null, null, null, 0xFF000000, 0.75, true, InnerLines: false),
        new("boxListBlue", "Box List Blue", 0xFF2563EB, 0xFFFFFFFF, 0xFFDBEAFE, null, 0xFF2563EB, 0.5, true),
        new("boxListGreen", "Box List Green", 0xFF16A34A, 0xFFFFFFFF, 0xFFDCFCE7, null, 0xFF16A34A, 0.5, true),
        new("boxListRed", "Box List Red", 0xFFDC2626, 0xFFFFFFFF, 0xFFFEE2E2, null, 0xFFDC2626, 0.5, true),
        new("boxListYellow", "Box List Yellow", 0xFFCA8A04, 0xFFFFFFFF, 0xFFFEF9C3, null, 0xFFCA8A04, 0.5, true),
        new("elegant", "Elegant", 0xFF1F2937, 0xFFFFFFFF, null, null, 0xFF6B7280, 0.5, true),
        new("financial", "Financial", 0xFFE5E7EB, null, 0xFFF9FAFB, null, 0xFF9CA3AF, 0.5, true),
        new("simpleGridColumns", "Simple Grid Columns", null, null, null, null, 0xFF9CA3AF, 0.5, false),
        new("simpleGridRows", "Simple Grid Rows", 0xFFF3F4F6, null, 0xFFF9FAFB, null, 0xFFD1D5DB, 0.5, true),
        new("simpleListShaded", "Simple List Shaded", 0xFFD1D5DB, null, 0xFFF3F4F6, null, 0xFFD1D5DB, 0.25, true, InnerLines: false),
    ];

    public TableLook? Find(string key) => All.FirstOrDefault(look => string.Equals(look.Key, key, StringComparison.Ordinal));

    public TableLook? Named(string? styleName) =>
        All.FirstOrDefault(look => string.Equals(look.Name, styleName, StringComparison.Ordinal) || string.Equals(Unbanded(look), styleName, StringComparison.Ordinal));

    public string Unbanded(TableLook look) => look.Name + " No Bands";
}
