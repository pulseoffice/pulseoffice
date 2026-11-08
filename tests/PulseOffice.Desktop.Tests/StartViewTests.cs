using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class StartViewTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    private sealed class OneRecent(string path) : IRecentDocuments
    {
        public IReadOnlyList<RecentDocument> Items { get; } = [new RecentDocument(path, "relazione.odt", new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero))];

        public void Remember(string path, string name)
        {
        }

        public void Forget(string path)
        {
        }

        public void Clear()
        {
        }

        public event EventHandler? Changed { add { } remove { } }
    }

    [Fact]
    public Task Un_percorso_lungo_nei_recenti_si_accorcia_e_lascia_vedere_la_data() => Session.Dispatch(() =>
    {
        var path = "/" + string.Join("/", Enumerable.Range(0, 30).Select(index => $"cartella-molto-lunga-{index}")) + "/relazione.odt";
        var view = new StartView(new OneRecent(path), new TextCatalog(UiLanguage.Italian), new IconCatalog());
        var window = new Window { Width = 1000, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var folder = view.GetVisualDescendants().OfType<TextBlock>().Single(block => ToolTip.GetTip(block) as string == path);
        var date = view.GetVisualDescendants().OfType<TextBlock>().Single(block => Grid.GetColumn(block) == 2 && block.Parent is Grid { ColumnDefinitions.Count: 3 });
        var windowRight = window.Bounds.Width;
        Assert.True(Avalonia.VisualExtensions.TranslatePoint(folder, new Avalonia.Point(folder.Bounds.Width, 0), window)!.Value.X <= windowRight);
        Assert.True(Avalonia.VisualExtensions.TranslatePoint(date, new Avalonia.Point(date.Bounds.Width, 0), window)!.Value.X <= windowRight);
        Assert.True(date.Bounds.Width > 0);
        window.Close();
    }, CancellationToken.None);
}

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DisabledFieldThemeTests(HeadlessDesktopSession headless)
{
    [Fact]
    public Task I_campi_spenti_seguono_il_tema_scuro() => headless.Session.Dispatch(() =>
    {
        var application = Avalonia.Application.Current!;
        PulseOffice.UI.Theming.Palette.Apply(application, true);
        try
        {
            var text = new TextBox { IsEnabled = false, Text = "12" };
            var combo = new ComboBox { IsEnabled = false, ItemsSource = new[] { "a" } };
            var list = new ListBox { ItemsSource = new[] { "a" }, Height = 60 };
            var window = new Window { Width = 400, Height = 200, Content = new StackPanel { Children = { text, combo, list } } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            application.TryGetResource("PulseSurfaceSunken", Avalonia.Styling.ThemeVariant.Dark, out var expected);
            var sunken = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(expected).Color;
            var textBorder = text.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_BorderElement");
            var comboBorder = combo.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Background");
            Assert.Equal(sunken, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(textBorder.Background).Color);
            Assert.Equal(sunken, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(comboBorder.Background).Color);
            Assert.Equal(sunken, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(list.Background).Color);
            window.Close();
        }
        finally
        {
            PulseOffice.UI.Theming.Palette.Apply(application, false);
        }
    }, CancellationToken.None);
}
