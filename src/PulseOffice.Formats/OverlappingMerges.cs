using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats;

public sealed class OverlappingMerges(string code)
{
    private readonly Diagnostic _loss = Diagnostic.Warning(code,
        "Merged ranges that overlap an earlier merged range in the file are not valid: each one replaces the merge it overlaps.");

    public void Add(MergedRegions merges, CellRange region, ICollection<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(merges);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!region.IsSingleCell && merges.Overlapping(region).Any() && !diagnostics.Any(diagnostic => diagnostic.Code == _loss.Code))
            diagnostics.Add(_loss);
        merges.Add(region);
    }
}
