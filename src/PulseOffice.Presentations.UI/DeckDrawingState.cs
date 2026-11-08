namespace PulseOffice.Presentations.UI;

public enum DeckPen { None, Polygon, PolygonConstrained, Curve, Freehand }

public interface IDeckDrawingState
{
    DeckPen Pen { get; }

    bool Filled { get; }

    void Choose(DeckPen pen, bool filled);

    void Finish();

    bool EditingPoints { get; }

    PulseOffice.Drawing.Freeform.PathHandle Handle { get; set; }

    void ToggleEditingPoints();

    bool GluePoints { get; }

    (string ObjectId, PulseOffice.Drawing.Shapes.ConnectorSite Site)? GlueStart { get; set; }

    void ToggleGluePoints();

    event EventHandler? Changed;
}

public sealed class DeckDrawingState : IDeckDrawingState
{
    public DeckPen Pen { get; private set; } = DeckPen.None;

    public bool Filled { get; private set; }

    public event EventHandler? Changed;

    public void Choose(DeckPen pen, bool filled)
    {
        Pen = pen;
        Filled = filled;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool EditingPoints { get; private set; }

    public PulseOffice.Drawing.Freeform.PathHandle Handle
    {
        get => _handle;
        set
        {
            if (_handle == value) return;
            _handle = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private PulseOffice.Drawing.Freeform.PathHandle _handle = PulseOffice.Drawing.Freeform.PathHandle.None;

    public bool GluePoints { get; private set; }

    public (string ObjectId, PulseOffice.Drawing.Shapes.ConnectorSite Site)? GlueStart { get; set; }

    public void ToggleGluePoints()
    {
        GluePoints = !GluePoints;
        GlueStart = null;
        if (GluePoints)
        {
            Pen = DeckPen.None;
            EditingPoints = false;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleEditingPoints()
    {
        EditingPoints = !EditingPoints;
        if (EditingPoints) GluePoints = false;
        _handle = PulseOffice.Drawing.Freeform.PathHandle.None;
        if (EditingPoints) Pen = DeckPen.None;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Finish()
    {
        if (Pen == DeckPen.None) return;
        Pen = DeckPen.None;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
