using PulseOffice.Testing;
using PulseOffice.Document.Forms;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Forms;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class AuditNineteenTests
{
    [Fact]
    public void Il_nome_predefinito_di_un_controllo_di_modulo_segue_la_lingua_e_salta_quelli_presi()
    {
        var texts = new TextCatalog(UiLanguage.Italian);
        var naming = new FormControlNaming(texts);

        Assert.Equal("Casella di controllo 1", naming.NextName(FormControlKind.CheckBox, []));
        Assert.Equal("Casella di controllo 3", naming.NextName(FormControlKind.CheckBox, ["Casella di controllo 1", "casella di controllo 2"]));

        texts.Use(UiLanguage.English);
        Assert.Equal("Push Button 1", naming.NextName(FormControlKind.PushButton, []));

        texts.Use(UiLanguage.German);
        Assert.Equal("Markierfeld 1", naming.NextName(FormControlKind.CheckBox, []));
    }

    [Fact]
    public void Ogni_tipo_di_controllo_ha_un_nome_predefinito_in_ogni_lingua()
    {
        foreach (var language in new[] { UiLanguage.Italian, UiLanguage.English, UiLanguage.German, UiLanguage.Spanish, UiLanguage.French })
        {
            var texts = new TextCatalog(language);
            foreach (var kind in Enum.GetValues<FormControlKind>())
                Assert.True(texts.Contains($"form.defaultName.{kind}"), $"{language} {kind}");
        }
    }
}

public sealed class WindowRegionCycleTests
{
    private sealed class Region(string name, List<string> log, bool available = true) : PulseOffice.UI.Controls.Navigation.IWindowRegion
    {
        public bool IsAvailable => available;

        public bool ContainsFocus => log.Count > 0 && log[^1] == name;

        public bool Focus()
        {
            log.Add(name);
            return true;
        }
    }

    [Fact]
    public void F6_gira_fra_le_aree_disponibili_e_Maiusc_F6_torna_indietro()
    {
        var log = new List<string> { "documento" };
        var regions = new[]
        {
            new Region("ribbon", log), new Region("documento", log), new Region("pannello", log, available: false), new Region("stato", log),
        };
        var cycle = new PulseOffice.UI.Controls.Navigation.WindowRegionCycle();

        cycle.Move(regions, backwards: false);
        cycle.Move(regions, backwards: false);
        cycle.Move(regions, backwards: false);
        cycle.Move(regions, backwards: true);

        Assert.Equal(["documento", "stato", "ribbon", "documento", "ribbon"], log);
    }
}

public sealed class ShapeTextPendingFormatTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static PulseOffice.Drawing.Shapes.ShapeObject Bolder(PulseOffice.Drawing.Shapes.ShapeObject shape, int from, int to) =>
        new PulseOffice.Drawing.Shapes.ShapeTextEditing().Format(shape, from, to, run => run with { Bold = true });

    private static PulseOffice.UI.Controls.Shapes.ShapeTextEditor Editing(string text, int caret)
    {
        var editor = new PulseOffice.UI.Controls.Shapes.ShapeTextEditor();
        var window = new Avalonia.Controls.Window { Content = new Avalonia.Controls.Canvas { Children = { editor } } };
        window.Show();
        editor.Begin(new PulseOffice.UI.Controls.Shapes.ShapeTextEditStart("forma",
            new PulseOffice.Drawing.Shapes.ShapeObject { Id = "forma", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle, Text = text },
            new PulseOffice.UI.Controls.Shapes.ShapeTextPlacement(new Avalonia.Rect(0, 0, 200, 100), 1),
            new PulseOffice.UI.Controls.Shapes.ShapeSurroundings()));
        editor.Box.SelectionStart = editor.Box.SelectionEnd = editor.Box.CaretIndex = caret;
        return editor;
    }

    [Fact]
    public Task Con_il_cursore_in_una_parola_il_formato_vale_per_la_parola() => Session.Dispatch(() =>
    {
        var editor = Editing("uno due tre", 5);
        Assert.Equal((4, 7), editor.FormatRange());
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Fuori_da_una_parola_il_formato_resta_in_sospeso_per_il_testo_che_si_scrive() => Session.Dispatch(() =>
    {
        var editor = Editing("uno ", 4);
        Assert.Null(editor.FormatRange());
        editor.Pend(Bolder);
        editor.Box.Text = "uno d";
        editor.Box.SelectionStart = editor.Box.SelectionEnd = editor.Box.CaretIndex = 5;
        editor.Box.Text = "uno du";
        var runs = editor.Live!.Runs!;
        Assert.Equal("uno ", string.Concat(runs.Where(run => !run.Bold).Select(run => run.Text)));
        Assert.Equal("du", string.Concat(runs.Where(run => run.Bold).Select(run => run.Text)));
        Assert.False(editor.HasPendingFormat);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Spostare_il_cursore_senza_scrivere_annulla_il_formato_in_sospeso() => Session.Dispatch(() =>
    {
        var editor = Editing("uno ", 4);
        editor.Pend(Bolder);
        editor.Box.SelectionStart = editor.Box.SelectionEnd = editor.Box.CaretIndex = 0;
        Assert.False(editor.HasPendingFormat);
    }, TestContext.Current.CancellationToken);
}

public sealed class UnreadableFileMessageTests
{
    [Theory]
    [InlineData("ODF003")]
    [InlineData("ODT101")]
    [InlineData("ODP101")]
    [InlineData("XLSX002")]
    [InlineData("PPTX101")]
    [InlineData("DOCX107")]
    public void Un_file_rotto_si_spiega_nella_lingua_dell_interfaccia_senza_il_testo_tecnico(string code)
    {
        var texts = new PulseOffice.Localization.DiagnosticTexts(new TextCatalog(UiLanguage.Italian));
        var text = texts.Describe(PulseOffice.Foundation.Diagnostics.Diagnostic.Error(code, "Unreadable package").With("End of Central Directory record could not be found."));
        Assert.Contains("danneggiato o incompleto", text);
        Assert.DoesNotContain("Central Directory", text);
    }
}
