using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Notifications;

public sealed class NotificationLook
{
    public string Tone(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Error => Palette.Keys.Danger,
        NotificationSeverity.Warning => Palette.Keys.Warning,
        NotificationSeverity.Success => Palette.Keys.Success,
        _ => Palette.Keys.Info,
    };

    public string Icon(NotificationSeverity severity) => $"ui.notice.{Key(severity)}";

    public string NameKey(NotificationSeverity severity) => $"notification.severity.{Key(severity)}";

    private static string Key(NotificationSeverity severity) => severity.ToString().ToLowerInvariant();
}
