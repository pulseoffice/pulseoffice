using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Prompts;

public enum ZoomFit
{
    Page,
    Width,
}

public sealed record ZoomRequest(double CurrentPercent, double MinimumPercent, double MaximumPercent)
{
    public IReadOnlyList<ZoomFit> Fits { get; init; } = [];

    public ZoomFit? CurrentFit { get; init; }

    public double PageAspect { get; init; } = 1.414;
}

public sealed record ZoomAnswer(double? Percent, ZoomFit? Fit)
{
    public static ZoomAnswer At(double percent) => new(percent, null);

    public static ZoomAnswer Fitting(ZoomFit fit) => new(null, fit);
}

public interface IZoomPrompt
{
    Task<ZoomAnswer?> AskAsync(ZoomRequest request, CancellationToken ct);
}

public sealed class ZoomPrompt(IDialogService dialogs, ITextCatalog texts, IMeasureUnits units, IIconCatalog icons) : IZoomPrompt
{
    public async Task<ZoomAnswer?> AskAsync(ZoomRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var answer = await dialogs.ShowAsync<ZoomAnswer>(
            new ZoomView(request, texts, units, icons, CultureInfo.CurrentCulture),
            new DialogOptions { TitleKey = "zoom.title", PreferredWidth = 560 },
            ct).ConfigureAwait(true);

        if (answer?.Percent is { } percent)
            return double.IsFinite(percent) ? ZoomAnswer.At(Math.Clamp(percent, request.MinimumPercent, request.MaximumPercent)) : null;

        return answer;
    }
}
