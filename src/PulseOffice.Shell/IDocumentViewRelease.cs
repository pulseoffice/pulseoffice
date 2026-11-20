namespace PulseOffice.Shell;

public interface IDocumentViewRelease
{
    bool IsSharedModelReleased { get; }

    void ReleaseSharedModel();
}
