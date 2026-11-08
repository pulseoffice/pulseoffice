using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PulseOffice.Localization;
using PulseOffice.Platform;

namespace PulseOffice.Shell;

public interface IApplicationMenu
{
    NativeMenu Menu { get; }
}

public sealed class ApplicationMenu : IApplicationMenu
{
    private const string About = "shell.help.about";
    private const string CheckUpdates = "shell.help.checkUpdates";
    private const string Settings = "shell.tools.options";
    private const string Quit = "shell.file.quit";
    private const string ServicesMarker = "IsServicesSubmenu";

    private readonly ITextCatalog _texts;
    private readonly List<(NativeMenuItem Item, string TextKey)> _translated = [];

    public ApplicationMenu(ITextCatalog texts, Action<string> execute, IApplicationVisibility visibility, NativeMenu? menu = null)
    {
        Menu = menu ?? new NativeMenu();
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(visibility);

        Menu.Items.Clear();
        Add($"command.{About}.title", () => execute(About));
        Add($"command.{CheckUpdates}.title", () => execute(CheckUpdates));
        Menu.Items.Add(new NativeMenuItemSeparator());
        Add($"command.{Settings}.title", () => execute(Settings));

        if (visibility.IsSupported)
        {
            if (FindServicesMarker() is { } marker)
            {
                Menu.Items.Add(new NativeMenuItemSeparator());
                var services = Add("shell.appmenu.services", null);
                services.Menu = new NativeMenu();
                services.SetValue(marker, true);
            }
            Menu.Items.Add(new NativeMenuItemSeparator());
            Add("shell.appmenu.hide", visibility.HideApplication, new KeyGesture(Key.H, KeyModifiers.Meta));
            Add("shell.appmenu.hideOthers", visibility.HideOtherApplications, new KeyGesture(Key.H, KeyModifiers.Meta | KeyModifiers.Alt));
            Add("shell.appmenu.showAll", visibility.ShowAllApplications);
            Menu.Items.Add(new NativeMenuItemSeparator());
            Add("shell.appmenu.quit", () => execute(Quit), new KeyGesture(Key.Q, KeyModifiers.Meta));
        }

        _texts.LanguageChanged += (_, _) => Translate();
        Translate();
    }

    public NativeMenu Menu { get; }

    private NativeMenuItem Add(string textKey, Action? run, KeyGesture? gesture = null)
    {
        var item = new NativeMenuItem { Gesture = gesture };
        if (run is not null) item.Click += (_, _) => run();
        Menu.Items.Add(item);
        _translated.Add((item, textKey));
        return item;
    }

    private static AvaloniaProperty? FindServicesMarker() =>
        AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(typeof(NativeMenuItem))
            .FirstOrDefault(property => property.Name == ServicesMarker && property.PropertyType == typeof(bool));

    private void Translate()
    {
        foreach (var (item, textKey) in _translated) item.Header = _texts[textKey];
    }
}
