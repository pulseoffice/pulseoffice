using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum TraceKind { Precedent, Dependent, Error, Invalid }

public sealed record TraceArrow(CellAddress From, CellAddress To, TraceKind Kind)
{
    public bool IsExternal { get; init; }
}
