using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Links;

public interface ILinkFollower
{
    bool Follow(string? link, Func<string, bool> place);
}

public sealed class LinkFollower(IShellOpener opener, IStatusBarService status, ITextCatalog texts) : ILinkFollower
{
    public bool Follow(string? link, Func<string, bool> place)
    {
        ArgumentNullException.ThrowIfNull(place);
        if (link?.Trim() is not { Length: > 0 } target) return false;
        if (target.StartsWith('#')) return place(target);
        if (opener.OpenUrl(target).Succeeded) return true;
        status.Report(texts.Format("link.notOpened", target), NotificationSeverity.Warning);
        return false;
    }
}
