using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioAuditTests
{
    [Fact]
    public Task InSolaLetturaAnnullaDaTastieraNonCambiaIlDocumento() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        scene.ReadOnly = true;

        scene.Press(Key.Z, RawInputModifiers.Meta);

        Assert.Equal(["Testo"], scene.Paragraphs);
        scene.ReadOnly = false;
        scene.Press(Key.Z, RawInputModifiers.Meta);
        scene.Press(Key.Y, RawInputModifiers.Meta);
        scene.ReadOnly = true;
        scene.Press(Key.Z, RawInputModifiers.Meta);
        Assert.Equal(["Testo"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task InSolaLetturaLaComposizioneNonCompareNelDocumento() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene { ReadOnly = true };

        scene.View.SetPreedit("かな", 1);

        Assert.Null(scene.Editor.Preedit);
        Assert.Same(scene.Editor.Layout, scene.Editor.DisplayLayout);
    }, CancellationToken.None);

    [Fact]
    public Task IncollaConservaLaFormattazioneAncheSeGliAppuntiUsanoCrLf() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        scene.Type("Rosso");
        scene.Press(Key.Enter);
        scene.Type("Blu");
        scene.Editor.SelectAll();
        scene.Editor.Toggle(CharacterToggle.Bold);
        await scene.View.CopyAsync();
        await scene.Clipboard.WriteAsync([.. scene.Clipboard.Representations.Select(representation => representation.Flavor == PulseOffice.Exchange.ExchangeFlavor.Text
            ? PulseOffice.Exchange.ExchangeRepresentation.OfText(PulseOffice.Exchange.ExchangeFlavor.Text, representation.AsText().Replace("\n", "\r\n", StringComparison.Ordinal))
            : representation)]);
        scene.Press(Key.Down, RawInputModifiers.Meta);
        scene.Press(Key.Enter);
        scene.Type("x");
        if (scene.Editor.IsOn(CharacterToggle.Bold)) scene.Editor.Toggle(CharacterToggle.Bold);
        Assert.False(scene.Editor.IsOn(CharacterToggle.Bold));
        var before = scene.Editor.Caret;

        await scene.View.PasteAsync();
        Dispatcher.UIThread.RunJobs();

        scene.Editor.Select(before, before with { Offset = before.Offset + 5 });
        Assert.True(scene.Editor.IsOn(CharacterToggle.Bold));
    });
}
public sealed class FolioBlinkTests
{
    [Fact]
    public Task IlCursoreNonLampeggiaSenzaTastiera() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        Assert.True(scene.View.Canvas.IsBlinking);

        var other = new Avalonia.Controls.TextBox();
        ((Avalonia.Controls.Grid)scene.View).Children.Add(other);
        other.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.False(scene.View.Canvas.IsBlinking);

        scene.View.FocusDocument();
        Dispatcher.UIThread.RunJobs();
        Assert.True(scene.View.Canvas.IsBlinking);
        scene.Window.Content = null;
        Dispatcher.UIThread.RunJobs();
        Assert.False(scene.View.Canvas.IsBlinking);
    }, CancellationToken.None);
}

public sealed class GlyphMappingTests
{
    [Fact]
    public void UnCarattereSenzaGlifoRicadeDaSoloSulDisegnoDiSistema()
    {
        var mapping = new PulseOffice.Documents.UI.Rendering.FaceGlyphMapping();
        var glyphs = new Dictionary<int, int> { ['a'] = 5, [' '] = 3 };

        var missing = mapping.Map("a \U0001F600 a", [6, 3, 10, 0, 3, 6], code => glyphs.GetValueOrDefault(code));
        var present = mapping.Map("a a", [6, 3, 6], code => glyphs.GetValueOrDefault(code));

        Assert.False(missing.Complete);
        Assert.Equal(3, missing.Pieces.Count);
        Assert.Equal(2, missing.Pieces[0].Glyphs.Count);
        Assert.Equal("\U0001F600", missing.Pieces[1].Missing);
        Assert.Equal(9, missing.Pieces[1].X);
        Assert.Equal(19, missing.Pieces[2].X);
        Assert.Equal(2, missing.Pieces[2].Glyphs.Count);
        Assert.True(present.Complete);
        Assert.Equal(3, present.Count);
    }
}
