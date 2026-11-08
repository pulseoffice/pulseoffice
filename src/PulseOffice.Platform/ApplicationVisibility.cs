namespace PulseOffice.Platform;

public interface IApplicationVisibility
{
    bool IsSupported { get; }

    void HideApplication();

    void HideOtherApplications();

    void ShowAllApplications();
}

public sealed class UnsupportedApplicationVisibility : IApplicationVisibility
{
    public bool IsSupported => false;

    public void HideApplication()
    {
    }

    public void HideOtherApplications()
    {
    }

    public void ShowAllApplications()
    {
    }
}
