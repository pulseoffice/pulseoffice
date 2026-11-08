using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class TwoPaneDialogLayoutTests
{
    private static readonly TextCatalog Texts = new(UiLanguage.English);

    private static IReadOnlyList<FormField> Mixed() =>
    [
        new TextField("name", "Name"),
        new NumberField("size", "Size", 12) { Unit = "pt" },
        new ChoiceField("kind", "Kind", ["One", "Two"]),
        new CheckField("bold", "Bold"),
        new CheckField("italic", "Italic"),
        new CheckField("under", "Underline"),
    ];

    private static Window Show(Control content, double width)
    {
        var window = new Window { Width = width, Height = 700, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Rect BoundsIn(Control control, Visual root) =>
        control.TransformToVisual(root) is { } transform ? new Rect(control.Bounds.Size).TransformToAABB(transform) : default;

    [Fact]
    public Task LEtichettaStaAccantoAlCampoEdELeColonneSonoAllineate() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var form = new FormPromptView(string.Empty, Mixed(), Texts);
        var window = Show(form, 640);

        var rows = form.GetVisualDescendants().OfType<DialogRow>().Where(row => row.Label is not null).ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.False(row.IsStacked));

        var editorLefts = rows.Select(row => BoundsIn(row.Editor, form).Left).Distinct().ToList();
        Assert.Single(editorLefts);
        foreach (var row in rows)
        {
            var label = BoundsIn(row.Label!, form);
            var editor = BoundsIn(row.Editor, form);
            Assert.True(label.Right <= editor.Left);
            Assert.True(Math.Abs(label.Center.Y - editor.Center.Y) < editor.Height);
        }

        var check = form.GetVisualDescendants().OfType<CheckBox>().First();
        Assert.Equal(editorLefts[0], BoundsIn(check, form).Left, 1);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task InUnaFinestraStrettaLEtichettaVaSopraIlCampo() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var form = new FormPromptView(string.Empty, Mixed(), Texts);
        var window = Show(form, 300);

        var row = form.GetVisualDescendants().OfType<DialogRow>().First(candidate => candidate.Label is not null);
        Assert.True(row.IsStacked);
        Assert.True(BoundsIn(row.Label!, form).Bottom <= BoundsIn(row.Editor, form).Top + 0.5);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task SenzaSezioniDichiarateLeCaselleFormanoLaSezioneOpzioni() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var form = new FormPromptView(string.Empty, Mixed(), Texts);
        var sections = form.Layout.Sections;

        Assert.Equal(2, sections.Count);
        Assert.Null(sections[0].Title);
        Assert.Equal(3, sections[0].Rows.Count);
        Assert.Equal("Options", sections[1].Title);
        Assert.Equal(3, sections[1].Rows.Count);
    }, CancellationToken.None);

    [Fact]
    public Task UnaCasellaIsolataRestaNellaSezioneDeiCampi() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var form = new FormPromptView(string.Empty, [new TextField("a", "A"), new CheckField("b", "B"), new TextField("c", "C")], Texts);
        Assert.Single(form.Layout.Sections);
        Assert.Equal(3, form.Layout.Sections[0].Rows.Count);
    }, CancellationToken.None);

    [Fact]
    public Task LeSezioniDichiarateHannoIlLoroTitoloEIValoriRestanoTutti() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var form = new FormPromptView(string.Empty, Texts,
        [
            new FormSection("General", [new TextField("name", "Name", "x")]),
            new FormSection("Spacing", [new NumberField("before", "Before", 3), new NumberField("after", "After", 4)]),
        ]);

        Assert.Equal(["General", "Spacing"], form.Layout.Sections.Select(section => section.Title));
        Assert.Equal(3, form.Values.Count);
        Assert.Equal("x", form.Values["name"]);
    }, CancellationToken.None);

    [Fact]
    public Task LaLarghezzaSeguIlContenutoELAnteprimaLAllarga() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var small = new FormPromptView(string.Empty, [new NumberField("n", "Count", 1)], Texts);
        var mixed = new FormPromptView(string.Empty, Mixed(), Texts);
        var previewed = new FormPromptView(string.Empty, Mixed(), Texts) { Preview = new Border { Width = 200, Height = 120 } };
        var narrowPreview = new FormPromptView(string.Empty, Mixed(), Texts) { Preview = new Border { Width = 40, Height = 120 } };

        Assert.NotNull(small.PreferredWidth);
        Assert.True(small.PreferredWidth >= DialogLayoutMeasures.MinimumDialogWidth);
        Assert.True(small.PreferredWidth < 520);
        Assert.True(mixed.PreferredWidth >= small.PreferredWidth);
        Assert.True(narrowPreview.PreferredWidth > mixed.PreferredWidth);
        Assert.True(previewed.PreferredWidth >= narrowPreview.PreferredWidth + 150);
        Assert.True(previewed.PreferredWidth <= DialogLayoutMeasures.MaximumDialogWidth);
    }, CancellationToken.None);

    [Fact]
    public Task LAnteprimaStaADestraEScendeSottoQuandoMancaSpazio() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var preview = new Border { Width = 160, Height = 100 };
        var form = new FormPromptView(string.Empty, Mixed(), Texts) { Preview = preview };
        var window = Show(form, 760);

        Assert.False(form.Layout.IsPreviewBelow);
        var settings = BoundsIn(form.Layout.Sections[0], form);
        Assert.True(BoundsIn(preview, form).Left >= settings.Right);

        window.Width = 420;
        Dispatcher.UIThread.RunJobs();
        Assert.True(form.Layout.IsPreviewBelow);
        Assert.True(BoundsIn(preview, form).Top >= BoundsIn(form.Layout.Sections[^1], form).Bottom);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task SenzaAnteprimaNonCeRiquadroVuoto() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var form = new FormPromptView(string.Empty, Mixed(), Texts);
        var window = Show(form, 640);
        Assert.False(form.Layout.PreviewHost.IsVisible);
        Assert.Equal(form.Bounds.Width, BoundsIn(form.Layout.Sections[0], form).Width, 1);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task IlTabulatoreSegueLOrdineDeiCampi() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var form = new FormPromptView(string.Empty,
        [
            new TextField("a", "First"),
            new TextField("b", "Second"),
            new CheckField("c", "Third"),
        ], Texts);
        var window = Show(form, 640);
        var boxes = form.GetVisualDescendants().OfType<TextBox>().ToList();
        var check = form.GetVisualDescendants().OfType<CheckBox>().Single();

        boxes[0].Focus(NavigationMethod.Tab);
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(boxes[1].IsFocused);
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(check.IsFocused);
        Assert.Equal("Second", Avalonia.Automation.AutomationProperties.GetName(boxes[1]));
        window.Close();
    }, CancellationToken.None);
}
