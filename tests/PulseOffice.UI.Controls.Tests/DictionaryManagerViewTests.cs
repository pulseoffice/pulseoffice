using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;
using PulseOffice.Text.Spelling;
using PulseOffice.Text.Spelling.Dictionaries;
using PulseOffice.UI.Controls.Dictionaries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DictionaryManagerViewTests
{
    private sealed class FakeManager : IDictionaryManager
    {
        public HashSet<(string, DictionaryPart)> Installed { get; } = [];

        public List<DictionaryPackage> InstallRequests { get; } = [];

        public bool Offline { get; set; }

        public IReadOnlyList<DictionaryPart> SupportedParts => [DictionaryPart.Spelling, DictionaryPart.Hyphenation];

        public Task<DictionaryCatalog> CatalogAsync(bool refresh = false, CancellationToken ct = default) =>
            Task.FromResult(new DictionaryCatalog([Language("it"), Language("de"), Language("de-CH") with { Base = "de" }], Offline));

        public Task<OperationResult<bool>> InstallAsync(DictionaryPackage package, IProgress<DictionaryProgress>? progress = null, CancellationToken ct = default)
        {
            InstallRequests.Add(package);
            Installed.Add((package.Code, package.Part));
            return Task.FromResult(OperationResult<bool>.Ok(true));
        }

        public OperationResult<bool> Remove(string code, DictionaryPart part) =>
            Installed.Remove((code, part)) ? OperationResult<bool>.Ok(true) : OperationResult<bool>.Fail(Diagnostic.Error("DICT003", "missing").With(code));

        private DictionaryLanguage Language(string code) => new(code, LanguageTag.Parse(code),
        [
            State(code, DictionaryPart.Spelling, "MIT"),
            State(code, DictionaryPart.Hyphenation, "GPL-3.0"),
            Thesaurus(code),
        ]);

        private DictionaryPartState Thesaurus(string code)
        {
            var classifier = new DictionaryLicenseClassifier();
            DictionaryPackage[] alternatives =
            [
                new("wordnet", code, DictionaryPart.Thesaurus, "1", classifier.FromSpdx("CC-BY-4.0"), [], "WordNet"),
                new("openthesaurus", code, DictionaryPart.Thesaurus, "1", classifier.FromSpdx("LGPL-2.1-or-later"), [], "OpenThesaurus"),
            ];
            return new DictionaryPartState(DictionaryPart.Thesaurus, DictionaryStatus.Available, alternatives[0], null) { Alternatives = alternatives };
        }

        private DictionaryPartState State(string code, DictionaryPart part, string spdx)
        {
            var license = new DictionaryLicenseClassifier().FromSpdx(spdx);
            var package = new DictionaryPackage("fake", code, part, "1", license, [], "fake source");
            return Installed.Contains((code, part))
                ? new DictionaryPartState(part, DictionaryStatus.Installed, package, new InstalledDictionary(code, part, "fake", "1", license, "fake source", []))
                : new DictionaryPartState(part, DictionaryStatus.Available, package, null);
        }
    }

    private sealed record Scene(Window Window, DictionaryManagerView View, FakeManager Manager);

    private static Scene Open(FakeManager manager)
    {
        var texts = new TextCatalog(UiLanguage.English);
        var view = new DictionaryManagerView(new DictionaryManagerServices(manager, new IconCatalog(), texts, new DiagnosticTexts(texts)));
        var window = new Window { Width = 1100, Height = 800, Content = view };
        window.Show();
        Pump(window);
        return new Scene(window, view, manager);
    }

    private static void Pump(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static Button Find(Window window, string label) =>
        window.GetVisualDescendants().OfType<Button>()
            .First(button => button.IsEffectivelyVisible && ((button.Content as string) == label
                || button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label)));

    private static void Click(Window window, Control target)
    {
        var centre = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(centre);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Pump(window);
    }

    private static IEnumerable<string?> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text);

    [Fact]
    public Task MostraLeLingueConLeTreParti() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var scene = Open(new FakeManager());

        Assert.Equal(3, scene.View.Languages.Count);
        Assert.NotNull(scene.View.Selected);
        var texts = Texts(scene.Window).ToList();
        Assert.Contains("Spelling", texts);
        Assert.Contains("Hyphenation", texts);
        Assert.Contains("Thesaurus", texts);
        Assert.Contains("Free licence", texts);
        Assert.Contains("Copyleft", texts);
    }, CancellationToken.None);

    [Fact]
    public Task IlClicSuInstallaInstallaEAggiornaLoStato() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var scene = Open(new FakeManager());
        scene.View.Select("it");
        Pump(scene.Window);

        Click(scene.Window, Find(scene.Window, "Install"));

        Assert.Single(scene.Manager.InstallRequests);
        Assert.Contains("Installed", Texts(scene.Window));
        Assert.Contains("Remove", scene.Window.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string));
    }, CancellationToken.None);

    [Fact]
    public Task LaRicercaFiltraLeLingue() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var scene = Open(new FakeManager());
        var search = scene.Window.GetVisualDescendants().OfType<TextBox>().First();

        search.Text = "zzz";
        Pump(scene.Window);

        Assert.Contains("No language matches the search.", Texts(scene.Window));
    }, CancellationToken.None);

    [Fact]
    public Task SenzaReteLoDice() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var scene = Open(new FakeManager { Offline = true });

        Assert.Contains(Texts(scene.Window), text => text?.Contains("cannot be reached", StringComparison.Ordinal) == true);
    }, CancellationToken.None);

    [Fact]
    public Task SiPuoScegliereUnAltraFonte() => MacroEventsViewTests.Session.Dispatch(async () =>
    {
        var scene = Open(new FakeManager());
        scene.View.Select("it");
        Pump(scene.Window);

        var picker = Assert.Single(scene.Window.GetVisualDescendants().OfType<ComboBox>(), box => (box.Tag as string) == "dictionaries.source");
        Assert.Equal("wordnet", ((DictionaryPackage)picker.SelectedItem!).SourceId);

        picker.SelectedIndex = 1;
        Pump(scene.Window);
        await scene.View.InstallAsync(DictionaryPart.Thesaurus);

        Assert.Equal("openthesaurus", Assert.Single(scene.Manager.InstallRequests).SourceId);
    }, CancellationToken.None);

    [Fact]
    public Task LeVariantiStannoInUnaRigaSolaConIlSelettore() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var scene = Open(new FakeManager());

        var rows = scene.Window.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is "de" or "de-CH").ToList();
        Assert.Equal(["de"], rows.Select(button => (string)button.Tag!));

        Click(scene.Window, rows[0]);
        Assert.Equal("de", scene.View.Selected);
        var swiss = scene.Window.GetVisualDescendants().OfType<Button>().Single(button => (button.Tag as string) == "variant:de-CH");
        Click(scene.Window, swiss);

        Assert.Equal("de-CH", scene.View.Selected);
        Assert.Equal(2, scene.View.Languages.Count(language => language.Base == "de"));
    }, CancellationToken.None);
}
