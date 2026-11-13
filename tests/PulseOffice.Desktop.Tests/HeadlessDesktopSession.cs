using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Xunit;

namespace PulseOffice.Desktop.Tests;

public sealed class HeadlessDesktopApp
{
    private static readonly string[] ThemeFamilies = ["Arial", "Courier New", "Georgia", "Trebuchet MS", "Verdana"];

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().WithInterFont()
            .With(new Avalonia.Media.FontManagerOptions
            {
                FontFamilyMappings = ThemeFamilies.ToDictionary(family => family, _ => new Avalonia.Media.FontFamily("fonts:Inter#Inter"), StringComparer.OrdinalIgnoreCase),
            })
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class HeadlessDesktopSession
{
    public UiThreadSession Session { get; } = UiThreadSession.StartNew(typeof(HeadlessDesktopApp));
}

[CollectionDefinition(Name)]
public sealed class HeadlessDesktopCollection : ICollectionFixture<HeadlessDesktopSession>
{
    public const string Name = "Headless desktop";
}
