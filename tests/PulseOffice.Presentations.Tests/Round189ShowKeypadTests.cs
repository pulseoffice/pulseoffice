using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round189ShowKeypadTests
{
    [Fact]
    public void Black_and_white_screens_toggle_and_switch()
    {
        var keypad = new ShowKeypad();

        keypad.Toggle(ShowCurtain.Black);
        Assert.Equal(ShowCurtain.Black, keypad.Screen);
        keypad.Toggle(ShowCurtain.White);
        Assert.Equal(ShowCurtain.White, keypad.Screen);
        keypad.Toggle(ShowCurtain.White);
        Assert.Equal(ShowCurtain.Slide, keypad.Screen);
    }

    [Fact]
    public void Typed_digits_give_a_number_once()
    {
        var keypad = new ShowKeypad();
        keypad.Type(1);
        keypad.Type(2);

        Assert.Equal(12, keypad.TakeNumber());
        Assert.Null(keypad.TakeNumber());
    }
}
