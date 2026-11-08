using PulseOffice.Foundation.Zoom;
using PulseOffice.Document.Tables;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public enum CanvasPointerTool { Select, Pan, Zoom }

public sealed class DeckViewOptions
{
    public ZoomHistory ZoomHistory { get; } = new();

    public CanvasPointerTool Tool { get; private set; }

    public Action? ZoomToSelection { get; set; }

    public void SetTool(CanvasPointerTool tool) => Apply(() => Tool = Tool == tool ? CanvasPointerTool.Select : tool);

    public bool SlidePane { get; private set; } = true;

    public bool NotesPane { get; private set; }

    public bool ShowComments { get; private set; } = true;

    public bool ColorBar { get; private set; }

    private OpenGroup? _openGroup;

    public OpenGroup? OpenGroup
    {
        get => _openGroup;
        set => Apply(() => _openGroup = value);
    }

    public (string ElementId, PulseOffice.Document.Tables.TableCellRange Range)? TableCells { get; private set; }

    public void SetTableCells((string ElementId, PulseOffice.Document.Tables.TableCellRange Range)? cells) => Apply(() => TableCells = cells);

    public void ToggleColorBar() => Apply(() => ColorBar = !ColorBar);

    public SlideOutputQuality Quality { get; private set; } = SlideOutputQuality.Color;

    public event EventHandler? Changed;

    public void ToggleSlidePane() => Apply(() => SlidePane = !SlidePane);

    public void ToggleNotesPane() => Apply(() => NotesPane = !NotesPane);

    public void ToggleComments() => Apply(() => ShowComments = !ShowComments);

    public void SetQuality(SlideOutputQuality quality) => Apply(() => Quality = quality);

    private void Apply(Action change)
    {
        change();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record OpenGroup(string SlideId, IReadOnlyList<string> MemberIds)
{
    public string? SlideGroupId { get; init; }

    public bool IsValidIn(Presentation deck) =>
        deck.Slides.FirstOrDefault(slide => slide.Id == SlideId) is { } slide
        && MemberIds.All(id => slide.Find(id) is not null);
}
