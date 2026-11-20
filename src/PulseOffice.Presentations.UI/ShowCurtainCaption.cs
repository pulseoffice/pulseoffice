using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class ShowCurtainCaption
{
    private readonly TextBlock _block;
    private readonly string _endOfShow;
    private readonly string _countdownFormat;
    private int _secondsLeft = -1;

    public ShowCurtainCaption(TextBlock block, string? endOfShow, string? countdownFormat)
    {
        ArgumentNullException.ThrowIfNull(block);
        _block = block;
        _endOfShow = endOfShow ?? string.Empty;
        _countdownFormat = countdownFormat ?? string.Empty;
        _block.Text = _endOfShow;
    }

    public string TextFor(ShowCurtain screen) => screen switch
    {
        ShowCurtain.End => _endOfShow,
        ShowCurtain.Pause when _secondsLeft >= 0 && _countdownFormat.Length > 0 => string.Format(CultureInfo.CurrentCulture, _countdownFormat, _secondsLeft),
        _ => string.Empty,
    };

    public void Countdown(ShowCurtain screen, int seconds)
    {
        _secondsLeft = Math.Max(0, seconds);
        Refresh(screen);
    }

    public void Refresh(ShowCurtain screen)
    {
        if (screen != ShowCurtain.Pause) _secondsLeft = -1;
        var text = TextFor(screen);
        _block.Text = text;
        _block.VerticalAlignment = screen == ShowCurtain.Pause ? VerticalAlignment.Center : VerticalAlignment.Top;
        _block.IsVisible = text.Length > 0;
    }
}
