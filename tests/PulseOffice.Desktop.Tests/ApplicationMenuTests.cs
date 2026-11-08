using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ApplicationMenuTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    [Fact]
    public Task Il_menu_dell_applicazione_apre_informazioni_e_impostazioni_e_cambia_lingua() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.Italian);
        var executed = new List<string>();
        var menu = new ApplicationMenu(texts, executed.Add, new UnsupportedApplicationVisibility()).Menu;
        var items = menu.Items.OfType<NativeMenuItem>().Where(item => item is not NativeMenuItemSeparator).ToList();

        Assert.Equal([texts["command.shell.help.about.title"], texts["command.shell.help.checkUpdates.title"], texts["command.shell.tools.options.title"]], items.Select(item => item.Header));
        Assert.Equal("Informazioni su PulseOffice", items[0].Header);
        foreach (var item in items) ((INativeMenuItemExporterEventsImplBridge)item).RaiseClicked();
        Assert.Equal(["shell.help.about", "shell.help.checkUpdates", "shell.tools.options"], executed);

        texts.Use(UiLanguage.English);
        Assert.Equal(texts["command.shell.help.about.title"], items[0].Header);
        Assert.NotEqual("Informazioni su PulseOffice", items[0].Header);
    }, CancellationToken.None);

    [Fact]
    public Task Nascondi_mostra_tutte_ed_esci_sono_voci_nostre_tradotte_che_seguono_la_lingua() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.Italian);
        var executed = new List<string>();
        var visibility = new RecordingVisibility();
        var menu = new ApplicationMenu(texts, executed.Add, visibility).Menu;
        NativeMenuItem Find(string header) => menu.Items.OfType<NativeMenuItem>().Single(item => item.Header == header);

        ((INativeMenuItemExporterEventsImplBridge)Find("Nascondi PulseOffice")).RaiseClicked();
        ((INativeMenuItemExporterEventsImplBridge)Find("Nascondi altre")).RaiseClicked();
        ((INativeMenuItemExporterEventsImplBridge)Find("Mostra tutte")).RaiseClicked();
        ((INativeMenuItemExporterEventsImplBridge)Find("Esci da PulseOffice")).RaiseClicked();
        Assert.Equal(["hide", "hideOthers", "showAll"], visibility.Calls);
        Assert.Equal(["shell.file.quit"], executed);

        foreach (var language in new[] { UiLanguage.English, UiLanguage.German, UiLanguage.Spanish, UiLanguage.French })
        {
            texts.Use(language);
            var headers = menu.Items.OfType<NativeMenuItem>().Where(item => item is not NativeMenuItemSeparator).Select(item => item.Header).ToList();
            Assert.DoesNotContain("Nascondi PulseOffice", headers);
            Assert.All(headers, header => Assert.False(string.IsNullOrWhiteSpace(header)));
        }
        texts.Use(UiLanguage.English);
        Assert.Contains(menu.Items.OfType<NativeMenuItem>(), item => item.Header == "Hide Others");
        Assert.Contains(menu.Items.OfType<NativeMenuItem>(), item => item.Header == "Quit PulseOffice");
    }, CancellationToken.None);

    [Fact]
    public void Le_voci_inglesi_di_Avalonia_nel_menu_dell_applicazione_sono_disattivate()
    {
        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PulseOffice.Desktop", "Program.cs"));
        Assert.Contains("DisableDefaultApplicationMenuItems = true", program, StringComparison.Ordinal);
    }

    private sealed class RecordingVisibility : IApplicationVisibility
    {
        public List<string> Calls { get; } = [];

        public bool IsSupported => true;

        public void HideApplication() => Calls.Add("hide");

        public void HideOtherApplications() => Calls.Add("hideOthers");

        public void ShowAllApplications() => Calls.Add("showAll");
    }

    [Fact]
    public void L_applicazione_si_chiama_PulseOffice_nel_menu_di_sistema()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PulseOffice.Desktop", "App.axaml"));
        Assert.Contains("Name=\"PulseOffice\"", xaml, StringComparison.Ordinal);
    }
}
