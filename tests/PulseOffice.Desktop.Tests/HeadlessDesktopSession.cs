using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Xunit;

namespace PulseOffice.Desktop.Tests;

public sealed class HeadlessDesktopApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
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
