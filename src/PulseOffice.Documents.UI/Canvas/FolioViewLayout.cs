namespace PulseOffice.Documents.UI.Canvas;

public enum FolioViewMode { Print, Web, Draft }

public interface IFolioViewLayout
{
    FolioViewMode Mode { get; set; }

    DeskLayout Desk { get; set; }

    bool HorizontalRuler { get; set; }

    bool VerticalRuler { get; set; }

    bool HorizontalScrollBar { get; set; }

    bool VerticalScrollBar { get; set; }

    bool Grid { get; set; }

    bool Whitespace { get; set; }

    bool HeaderFooterMenu { get; set; }

    bool BlockSelection { get; set; }

    bool DirectCursor { get; set; }

    bool SnapToGrid { get; set; }

    bool Helplines { get; set; }

    bool BaselineGrid { get; set; }

    double GridSpacing { get; }

    event EventHandler? Changed;
}

public sealed class FolioViewLayout : IFolioViewLayout
{
    public FolioViewMode Mode { get; set => Set(ref field, value); }

    public DeskLayout Desk { get; set => Set(ref field, value); }

    public bool HorizontalRuler { get; set => Set(ref field, value); } = true;

    public bool VerticalRuler { get; set => Set(ref field, value); } = true;

    public bool HorizontalScrollBar { get; set => Set(ref field, value); } = true;

    public bool VerticalScrollBar { get; set => Set(ref field, value); } = true;

    public bool Grid { get; set => Set(ref field, value); }

    public bool Whitespace { get; set => Set(ref field, value); } = true;

    public bool HeaderFooterMenu { get; set => Set(ref field, value); } = true;

    public bool BlockSelection { get; set => Set(ref field, value); }

    public bool DirectCursor { get; set => Set(ref field, value); }

    public bool SnapToGrid { get; set => Set(ref field, value); }

    public bool Helplines { get; set => Set(ref field, value); }

    public bool BaselineGrid { get; set => Set(ref field, value); }

    public double GridSpacing => 0.5 * 72 / 2.54;

    public event EventHandler? Changed;

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
