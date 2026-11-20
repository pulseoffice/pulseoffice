using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI;

public interface ILedgerFindScope
{
    CellRange? For(CellRange selection, bool unrestricted);

    void Landed(CellAddress? address);
}

public sealed class LedgerFindScope : ILedgerFindScope
{
    private CellRange? _scope;
    private CellAddress? _landed;

    public CellRange? For(CellRange selection, bool unrestricted)
    {
        if (unrestricted)
        {
            _scope = null;
            _landed = null;
            return null;
        }

        if (!selection.IsSingleCell)
        {
            _scope = selection;
            _landed = null;
            return selection;
        }

        if (_scope is { } kept && _landed == selection.Start && kept.Contains(selection.Start)) return kept;
        _scope = null;
        _landed = null;
        return null;
    }

    public void Landed(CellAddress? address)
    {
        if (_scope is not null) _landed = address;
    }
}
