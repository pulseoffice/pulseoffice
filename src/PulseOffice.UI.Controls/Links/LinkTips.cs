using Avalonia.Controls;

namespace PulseOffice.UI.Controls.Links;

public interface ILinkTips
{
    string? Text(string? link, string? tip);

    void Show(Control target, string? text);
}

public sealed class LinkTips : ILinkTips
{
    public string? Text(string? link, string? tip) =>
        link is not { Length: > 0 } ? null : tip is { Length: > 0 } ? tip : link;

    public void Show(Control target, string? text)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Equals(ToolTip.GetTip(target), text)) return;
        ToolTip.SetTip(target, text);
        ToolTip.SetIsOpen(target, text is not null);
    }
}
