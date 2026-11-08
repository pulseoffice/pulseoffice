using System.Reflection;
using System.Runtime.InteropServices;
using PulseOffice.Drawing.Assets;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell.About;

public interface IAboutDialog
{
    AboutFacts Facts();

    Task ShowAsync(CancellationToken cancellationToken = default);
}

public sealed class AboutDialog(IDialogService dialogs, ITextCatalog texts) : IAboutDialog
{
    public AboutFacts Facts()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AboutDialog).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString() ?? "0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus > 0) version = version[..plus];
        return new AboutFacts(
            version,
            RuntimeInformation.FrameworkDescription,
            $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            [.. AssetCatalog.BundledIcons, .. AssetCatalog.BundledStickers]);
    }

    public Task ShowAsync(CancellationToken cancellationToken = default) =>
        dialogs.ShowAsync<object>(new AboutView(Facts(), texts),
            new DialogOptions { TitleKey = "command.shell.help.about.title", PreferredWidth = 520 }, cancellationToken);
}
