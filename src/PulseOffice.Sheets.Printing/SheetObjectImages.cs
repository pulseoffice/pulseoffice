using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Sheets.Printing;

public sealed record SheetObjectImage(PageImage Image, DocumentRect Area);

public interface ISheetObjectImages
{
    SheetObjectImage? Image(EmbeddedObject content, DocumentRect bounds);
}

public sealed class NoSheetObjectImages : ISheetObjectImages
{
    public SheetObjectImage? Image(EmbeddedObject content, DocumentRect bounds) => null;
}
