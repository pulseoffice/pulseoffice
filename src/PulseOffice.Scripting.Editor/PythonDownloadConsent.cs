using System.Globalization;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.Scripting.CPython;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Scripting.Editor;

public sealed class PythonDownloadConsent(IDialogService dialogs, IStatusBarService status, ITextCatalog texts) : IPythonDownloadConsent
{
    private BackgroundActivity? _activity;

    public IProgress<ProvisioningProgress> Progress => new DispatchedProgress(Report);

    public Task<bool> AllowAsync(PythonDownloadOffer offer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return Dispatcher.UIThread.InvokeAsync(() => AskAsync(offer, ct));
    }

    private async Task<bool> AskAsync(PythonDownloadOffer offer, CancellationToken ct)
    {
        var megabytes = Math.Max(1, (int)Math.Round(offer.Bytes / 1048576.0)).ToString(CultureInfo.CurrentCulture);
        var view = new ChoicePromptView(
            texts.Format("macro.python.download.question", offer.Version),
            texts.Format("macro.python.download.explanation", megabytes, offer.Source, offer.Destination),
            [
                new PromptChoice(texts["macro.python.download.accept"], true, IsDefault: true),
                new PromptChoice(texts["macro.python.download.decline"], false, IsCancel: true),
            ],
            texts);

        var chosen = await dialogs.ShowAsync<object>(view, new DialogOptions
        {
            TitleKey = "macro.python.download.title",
            PreferredWidth = 520,
            CanCancelWithEscape = true,
        }, ct).ConfigureAwait(true);

        return chosen is true;
    }

    private void Report(ProvisioningProgress progress)
    {
        _activity ??= status.BeginActivity(texts["macro.python.download.activity"]);
        _activity.Progress = progress.Fraction;
        if (progress.Fraction < 1) return;

        status.EndActivity(_activity);
        _activity = null;
    }
}

internal sealed class DispatchedProgress(Action<ProvisioningProgress> report) : IProgress<ProvisioningProgress>
{
    public void Report(ProvisioningProgress value) => Dispatcher.UIThread.Post(() => report(value));
}
