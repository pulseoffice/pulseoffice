using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Drawing.Assets;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Prompts;

public interface IQrCodePrompt
{
    Task<VectorGraphicObject?> CreateAsync(string titleKey, CancellationToken ct);
}

public sealed class QrCodePrompt(IDialogService dialogs, IStatusBarService status, ITextCatalog texts, IQrEncoder encoder) : IQrCodePrompt
{
    private readonly QrErrorCorrection[] _levels =
        [QrErrorCorrection.Low, QrErrorCorrection.Medium, QrErrorCorrection.Quartile, QrErrorCorrection.High];

    public async Task<VectorGraphicObject?> CreateAsync(string titleKey, CancellationToken ct)
    {
        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                texts["qr.explanation"],
                [
                    new TextField("content", texts["qr.content"], "https://"),
                    new ChoiceField("correction", texts["qr.correction"],
                        [.. _levels.Select(level => texts[$"qr.level.{level.ToString().ToLowerInvariant()}"])], 1),
                    new NumberField("margin", texts["qr.margin"], 4, 0, 16),
                ],
                texts),
            new DialogOptions { TitleKey = titleKey }, ct).ConfigureAwait(true);

        if (values is null) return null;

        var content = (values["content"] as string ?? string.Empty).Trim();
        if (content.Length == 0) return null;

        var index = (int)Convert.ToInt64(values["correction"], CultureInfo.InvariantCulture);
        var level = _levels[Math.Clamp(index, 0, _levels.Length - 1)];

        if (!encoder.Fits(content, level))
        {
            status.Report(texts["status.qr.tooLong"], NotificationSeverity.Error);
            return null;
        }

        var margin = (int)Convert.ToInt64(values["margin"], CultureInfo.InvariantCulture);

        return new VectorGraphicObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Asset = new VectorAsset
            {
                Id = "qr",
                Name = content,
                SourceKind = VectorSourceKind.InlineSvg,
                SvgContent = encoder.ToSvg(encoder.Encode(content, level), margin),
            },
        };
    }
}
