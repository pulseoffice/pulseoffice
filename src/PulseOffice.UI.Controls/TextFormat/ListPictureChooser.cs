using System.Globalization;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public interface IListPictureChooser
{
    Task<ListBulletPicture?> ChooseAsync(CancellationToken ct);
}

public sealed class ListPictureChooser(
    IFileDialogService files,
    IChosenFileReader reader,
    IRasterMediaTypes mediaTypes,
    IStatusBarService status,
    ITextCatalog texts) : IListPictureChooser
{
    private const long Limit = 4L * 1024 * 1024;

    private const double Side = 12;

    public async Task<ListBulletPicture?> ChooseAsync(CancellationToken ct)
    {
        var chosen = await files.OpenFileAsync(
            texts["textFormat.list.picture.title"],
            [new FileFilter(texts["file.image"], [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"])],
            ct).ConfigureAwait(true);
        if (chosen is null) return null;
        await using (chosen.ConfigureAwait(true))
        {
            var content = await reader.ReadAsync(chosen, Limit, ct).ConfigureAwait(true);
            if (content.Status == ChosenFileReadStatus.TooLarge)
            {
                status.Report(texts.Format("status.image.tooLarge", (Limit / (1024 * 1024)).ToString(CultureInfo.CurrentCulture)), NotificationSeverity.Error);
                return null;
            }
            if (!content.Succeeded || Proportion(content.Bytes) is not { } proportion)
            {
                status.Report(texts["status.image.unreadable"], NotificationSeverity.Error);
                return null;
            }
            return new ListBulletPicture(content.Bytes, mediaTypes.MediaTypeOf(chosen.Name), Side * Math.Clamp(proportion, 0.25, 4), Side);
        }
    }

    private double? Proportion(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, false);
            using var bitmap = new Bitmap(stream);
            return bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0 ? (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height : null;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}
