using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtReadLosses
{

    public const string EmptyLinks = "ODF175";

    public const string MissingPictures = "ODF176";

    public const string UnreadObjects = "ODF177";

    public const string PictureBullets = "ODF178";

    public const string UnnamedBody = "ODT180";

    public const string ApproximatedShapes = "ODT181";

    private readonly IReadOnlyDictionary<string, Diagnostic> _losses = new Dictionary<string, Diagnostic>(StringComparer.Ordinal)
    {
        [EmptyLinks] = Diagnostic.Info(EmptyLinks, "Links without text or without a target, which do not lead anywhere, are imported as their plain text, or dropped when they are empty."),
        [MissingPictures] = Diagnostic.Warning(MissingPictures, "Images whose picture file is missing from the package are not imported, and neither are the frames that hold them."),
        [UnreadObjects] = Diagnostic.Warning(UnreadObjects, "Embedded objects that are neither charts nor formulas and have no picture to show, and OLE objects whose data is missing, are not imported, and neither are their frames."),
        [ApproximatedShapes] = Diagnostic.Warning(ApproximatedShapes, "Shapes whose geometry formulas cannot be evaluated, because their equations or path are malformed, are imported as rectangles."),
        [UnnamedBody] = Diagnostic.Warning(UnnamedBody, "The body of the document is not marked as text: it is read as text, as other ODF applications do. Check that nothing is missing."),
        [PictureBullets] = Diagnostic.Info(PictureBullets, "Picture bullets of lists whose picture is missing from the file are imported as character bullets."),
    };

    public IReadOnlyList<Diagnostic> Describe(IEnumerable<string> codes) =>
        [.. codes.Where(_losses.ContainsKey).Order(StringComparer.Ordinal).Select(code => _losses[code])];
}
