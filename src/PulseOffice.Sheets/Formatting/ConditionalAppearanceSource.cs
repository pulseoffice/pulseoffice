using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Formatting;

public interface IConditionalAppearanceSource
{
    ConditionalAppearance At(Worksheet sheet, CellAddress address);
}

public sealed class NoConditionalAppearance : IConditionalAppearanceSource
{
    public ConditionalAppearance At(Worksheet sheet, CellAddress address) => ConditionalAppearance.None;
}
