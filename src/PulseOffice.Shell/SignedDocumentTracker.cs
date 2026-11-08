using PulseOffice.Localization;
using PulseOffice.Security;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public interface ISignedDocumentTracker
{
    IReadOnlyList<DocumentSignature> Signatures { get; }

    string? Path { get; }

    void Start();

    void Refresh();

    event EventHandler? Changed;
}

public sealed class SignedDocumentTracker(IDocumentWorkspace workspace, IDocumentSignatureService signatures) : ISignedDocumentTracker
{
    private DateTime _readAt;
    private bool _started;

    public IReadOnlyList<DocumentSignature> Signatures { get; private set; } = [];

    public string? Path { get; private set; }

    public event EventHandler? Changed;

    public void Start()
    {
        if (_started) return;
        _started = true;

        workspace.ActiveChanged += (_, _) => Follow();
        workspace.ActiveUpdated += (_, _) => Follow();
        workspace.DocumentClosed += (_, _) => Follow();

        Follow();
    }

    public void Refresh()
    {
        Path = workspace.Active?.Path;
        _readAt = Stamp(Path);

        Signatures = Path is { Length: > 0 } path && signatures.CanSign(path)
            ? signatures.Read(path)
            : [];

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Follow()
    {
        var path = workspace.Active?.Path;

        if (string.Equals(path, Path, StringComparison.Ordinal) && Stamp(path) == _readAt) return;

        Refresh();
    }

    private DateTime Stamp(string? path) =>
        path is { Length: > 0 } && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
}

public sealed class SignatureStatusIndicator(
    ISignedDocumentTracker tracker,
    IStatusBarService status,
    ITextCatalog texts)
{
    public const string SegmentId = "shell.signature";

    public const string SignaturesCommand = "shell.file.signatures";

    private StatusSegment? _segment;

    public void Start()
    {
        if (_segment is not null) return;

        _segment = status.Register(SegmentId, StatusSegmentKind.Permanent);
        _segment.CommandId = SignaturesCommand;
        tracker.Changed += (_, _) => Show();
        tracker.Start();
        Show();
    }

    private void Show()
    {
        if (_segment is not { } segment) return;

        var found = tracker.Signatures;

        segment.Text = found.Count == 0
            ? string.Empty
            : found.Any(signature => signature.Status == SignatureStatus.Broken)
                ? texts["signature.indicatorBroken"]
                : found.All(signature => signature.Status == SignatureStatus.Valid)
                    ? texts["signature.indicator"]
                    : found.All(signature => signature.Status is SignatureStatus.Valid or SignatureStatus.UnverifiableInTime)
                        ? texts["signature.indicatorUnverifiableInTime"]
                        : texts["signature.indicatorUnverified"];
        segment.IsVisible = found.Count > 0;
    }
}
