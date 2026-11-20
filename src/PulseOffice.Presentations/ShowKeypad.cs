using System.Globalization;
using System.Text;

namespace PulseOffice.Presentations;

public enum ShowCurtain
{
    Slide,
    Black,
    White,
    End,
    Pause,
}

public sealed class ShowKeypad
{
    private const int MaxDigits = 5;
    private readonly StringBuilder _typed = new();

    public ShowCurtain Screen { get; private set; }

    public bool IsBlanked => Screen is ShowCurtain.Black or ShowCurtain.White;

    public string Typed => _typed.ToString();

    public void Toggle(ShowCurtain blank)
    {
        if (blank is not (ShowCurtain.Black or ShowCurtain.White)) throw new ArgumentOutOfRangeException(nameof(blank));
        Screen = Screen == blank ? ShowCurtain.Slide : blank;
    }

    public void Resume() => Screen = ShowCurtain.Slide;

    public void End() => Screen = ShowCurtain.End;

    public void Pause() => Screen = ShowCurtain.Pause;

    public void Type(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);
        if (_typed.Length < MaxDigits) _typed.Append((char)('0' + digit));
    }

    public void ClearTyped() => _typed.Clear();

    public int? TakeNumber()
    {
        if (_typed.Length == 0) return null;
        var number = int.Parse(_typed.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
        _typed.Clear();
        return number;
    }
}
