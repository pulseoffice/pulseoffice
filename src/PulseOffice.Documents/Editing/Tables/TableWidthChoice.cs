namespace PulseOffice.Documents.Editing.Tables;

public enum TableWidthMode
{
    Fixed,
    Percent,
    Automatic,
}

public sealed record TableWidthChoice(TableWidthMode Mode, double Points, double Percent);
