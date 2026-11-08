namespace PulseOffice.Sheets.UI;

public sealed class ActiveShellViewOptions(PulseOffice.Shell.IDocumentWorkspace workspace) : IShellViewOptions
{
    private IShellViewOptions? Current => workspace.Active?.View as IShellViewOptions;

    public bool FormulaBarVisible
    {
        get => Current?.FormulaBarVisible ?? true;
        set { if (Current is { } view) view.FormulaBarVisible = value; }
    }

    public bool SheetTabsVisible
    {
        get => Current?.SheetTabsVisible ?? true;
        set { if (Current is { } view) view.SheetTabsVisible = value; }
    }

    public bool HeadersVisible
    {
        get => Current?.HeadersVisible ?? true;
        set { if (Current is { } view) view.HeadersVisible = value; }
    }
}
