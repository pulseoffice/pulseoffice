namespace PulseOffice.UI.Controls.Review;

public enum ReviewPanelRequestKind
{
    Reply,
    Edit,
}

public sealed record ReviewPanelRequest(ReviewPanelRequestKind Kind, string Id, string Text);

public interface IReviewPanelRequests
{
    event EventHandler? Requested;

    void Request(ReviewPanelRequest request);

    ReviewPanelRequest? Take();
}

public sealed class ReviewPanelRequests : IReviewPanelRequests
{
    private ReviewPanelRequest? _pending;

    public event EventHandler? Requested;

    public void Request(ReviewPanelRequest request)
    {
        _pending = request ?? throw new ArgumentNullException(nameof(request));
        Requested?.Invoke(this, EventArgs.Empty);
    }

    public ReviewPanelRequest? Take()
    {
        var taken = _pending;
        _pending = null;
        return taken;
    }
}
