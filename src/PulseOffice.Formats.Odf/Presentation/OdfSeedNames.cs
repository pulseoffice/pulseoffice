using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfSeedNames
{
    public string Of(SlideLayout kind, PlaceholderRole role, int occurrence) => Of(string.Empty, kind, role, occurrence);

    public string Of(string masterPrefix, SlideLayout kind, PlaceholderRole role, int occurrence) =>
        occurrence <= 1 ? $"{masterPrefix}sd_{kind}_{role}" : $"{masterPrefix}sd_{kind}_{role}{occurrence}";

    public string MasterPrefix(string masterName) => masterName + "-";
}
