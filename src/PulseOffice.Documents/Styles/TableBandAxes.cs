namespace PulseOffice.Documents.Styles;

[Flags]
public enum TableBandAxes
{
    None = 0,
    Rows = 1,
    Columns = 2,
    Both = Rows | Columns,
}
