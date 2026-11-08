using Avalonia.Input;
using PulseOffice.UI.Controls.Shortcuts;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class ShortcutGestureTests
{
    [Fact]
    public void CtrlSignificaIlModificatoreDelSistema()
    {
        var gesture = ShortcutGesture.Parse("Ctrl+S");

        Assert.NotNull(gesture);
        Assert.Equal(Key.S, gesture.Value.Key);
        Assert.Equal(ShortcutGesture.Primary, gesture.Value.Modifiers);
    }

    [Fact]
    public void IModificatoriSiSommano()
    {
        var gesture = ShortcutGesture.Parse("Ctrl+Shift+P");

        Assert.NotNull(gesture);
        Assert.True(gesture.Value.Modifiers.HasFlag(KeyModifiers.Shift));
        Assert.True(gesture.Value.Modifiers.HasFlag(ShortcutGesture.Primary));
    }

    [Theory]
    [InlineData("Ctrl+0", Key.D0)]
    [InlineData("Ctrl+Plus", Key.OemPlus)]
    [InlineData("Ctrl+Minus", Key.OemMinus)]
    [InlineData("Ctrl+Slash", Key.OemQuestion)]
    [InlineData("F5", Key.F5)]
    public void INomiDeiTastiSiRiconoscono(string text, Key expected)
    {
        Assert.Equal(expected, ShortcutGesture.Parse(text)!.Value.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Inesistente")]
    public void UnaScorciatoiaIncomprensibileNonSiInventa(string text)
    {
        Assert.Null(ShortcutGesture.Parse(text));
    }

    [Fact]
    public void UnaCombinazioneConPiuModificatoriDelDovutoNonCorrisponde()
    {
        var gesture = ShortcutGesture.Parse("Ctrl+S")!.Value;

        var withShift = new KeyEventArgs
        {
            Key = Key.S,
            KeyModifiers = ShortcutGesture.Primary | KeyModifiers.Shift,
            RoutedEvent = InputElement.KeyDownEvent,
        };

        Assert.False(gesture.Matches(withShift));
    }

    [Fact]
    public void IlTastierinoNumericoValeComeIlTastoNormale()
    {
        var gesture = ShortcutGesture.Parse("Ctrl+Plus")!.Value;

        var numeric = new KeyEventArgs
        {
            Key = Key.Add,
            KeyModifiers = ShortcutGesture.Primary,
            RoutedEvent = InputElement.KeyDownEvent,
        };

        Assert.True(gesture.Matches(numeric));
    }

    [Fact]
    public void LaFormaMostrataSegueIlSistema()
    {
        var display = ShortcutGesture.Parse("Ctrl+Shift+P")!.Value.Display();

        if (OperatingSystem.IsMacOS()) Assert.Equal("⇧⌘P", display);
        else Assert.Equal("Ctrl+Shift+P", display);
    }

    [Fact]
    public void IlPiuComeTastoSiLeggeDopoUnModificatore()
    {
        var gesture = ShortcutGesture.Parse("Ctrl++");

        Assert.NotNull(gesture);
        Assert.Equal(Key.OemPlus, gesture.Value.Key);
        Assert.Equal(ShortcutGesture.Primary, gesture.Value.Modifiers);
    }

    [Theory]
    [InlineData("Ctrl+12")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Zorro")]
    public void UnGestoMalformatoNonSiLegge(string text) =>
        Assert.Null(ShortcutGesture.Parse(text));
}
