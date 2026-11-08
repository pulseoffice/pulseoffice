using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.Notifications;

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error,
}

public abstract record NotificationAction
{
    public abstract string LabelIn(ITextCatalog texts);
}

public sealed record CallbackNotificationAction(string Label, Action Run) : NotificationAction
{
    public override string LabelIn(ITextCatalog texts) => Label;
}

public sealed record CommandNotificationAction(string CommandId, string LabelKey) : NotificationAction
{
    public override string LabelIn(ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return texts[LabelKey];
    }
}

public static class NotificationRemedies
{
    public static CommandNotificationAction EditMode { get; } =
        new("shell.edit.editMode", "notification.action.editMode");

    public static CommandNotificationAction Save { get; } =
        new("shell.file.save", "notification.action.save");
}

public sealed record Notification(string Message, NotificationSeverity Severity, NotificationAction? Action = null)
{
    public bool IsProminent => Severity != NotificationSeverity.Info;

    public bool SameAs(Notification other) =>
        Severity == other.Severity && string.Equals(Message, other.Message, StringComparison.Ordinal);
}

public interface INotificationActionRunner
{
    void Run(NotificationAction action);
}
