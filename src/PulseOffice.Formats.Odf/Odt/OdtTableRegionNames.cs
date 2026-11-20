using PulseOffice.Documents.Styles;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtTableRegionNames
{
    private readonly IReadOnlyDictionary<TableRegion, string> _tokens = new Dictionary<TableRegion, string>
    {
        [TableRegion.TotalRow] = "total-row",
        [TableRegion.FirstColumn] = "first-column",
        [TableRegion.LastColumn] = "last-column",
        [TableRegion.NorthWestCell] = "top-left-cell",
        [TableRegion.NorthEastCell] = "top-right-cell",
        [TableRegion.SouthWestCell] = "bottom-left-cell",
        [TableRegion.SouthEastCell] = "bottom-right-cell",
        [TableRegion.HeaderRow] = "header-row",
        [TableRegion.FirstBand] = "first-band",
        [TableRegion.SecondBand] = "second-band",
    };

    public string Token(TableRegion region) => _tokens[region];

    public TableRegion? Region(string? token) =>
        _tokens.FirstOrDefault(entry => entry.Value == token) is { Value: not null } match ? match.Key : null;
}
