using Avalonia.Automation;
using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class ShowAnnouncer
{
    private readonly ITextCatalog? _texts;
    private readonly string _endOfShow;

    public ShowAnnouncer(ITextCatalog? texts, string endOfShow)
    {
        _texts = texts;
        _endOfShow = endOfShow;
        AutomationProperties.SetLiveSetting(Region, AutomationLiveSetting.Assertive);
    }

    public TextBlock Region { get; } = new()
    {
        Width = 1,
        Height = 1,
        Opacity = 0,
        IsHitTestVisible = false,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
    };

    public string Message => Region.Text ?? string.Empty;

    public void Screen(ShowCurtain screen, int number, int total)
    {
        switch (screen)
        {
            case ShowCurtain.Black:
                Say(Text("deck.present.announce.black"));
                break;
            case ShowCurtain.White:
                Say(Text("deck.present.announce.white"));
                break;
            case ShowCurtain.Pause:
                Say(Text("deck.present.announce.pause"));
                break;
            case ShowCurtain.End:
                Say(_endOfShow.Length > 0 ? _endOfShow : Text("deck.presenter.end"));
                break;
            default:
                Slide(number, total);
                break;
        }
    }

    public void Slide(int number, int total) =>
        Say(_texts?.Format("deck.present.announce.slide", number, total) ?? string.Empty);

    public void Typed(string digits) =>
        Say(_texts?.Format("deck.present.announce.typed", digits) ?? string.Empty);

    private string Text(string key) => _texts?[key] ?? string.Empty;

    private void Say(string message)
    {
        if (message.Length == 0) return;
        Region.Text = message;
        AutomationProperties.SetName(Region, message);
    }
}
