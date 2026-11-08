using Avalonia.Input;
using Avalonia.Input.Platform;
using PulseOffice.Exchange;
using PulseOffice.Platform;

namespace PulseOffice.UI.Controls.Services;

public interface ISystemClipboardText
{
    Task<string?> GetTextAsync();

    Task SetTextAsync(string text);
}

public interface IExchangeDataTransfers
{
    DataTransfer Write(IReadOnlyList<ExchangeRepresentation> representations);

    Task<IReadOnlyList<ExchangeRepresentation>> ReadAsync(IAsyncDataTransfer transfer);
}

public sealed class ExchangeDataTransfers(IRichClipboardFormatsProvider formats) : IExchangeDataTransfers
{
    private readonly IReadOnlyList<string> _packages =
    [
        ExchangeMediaTypes.Spreadsheet, ExchangeMediaTypes.Text, ExchangeMediaTypes.Presentation,
        ExchangeMediaTypes.Workbook, ExchangeMediaTypes.WordDocument, ExchangeMediaTypes.Slides,
    ];

    public DataTransfer Write(IReadOnlyList<ExchangeRepresentation> representations)
    {
        ArgumentNullException.ThrowIfNull(representations);
        var names = formats.Current;
        var item = new DataTransferItem();
        foreach (var representation in representations)
        {
            if (representation.Data.IsEmpty) continue;
            switch (representation.Flavor)
            {
                case ExchangeFlavor.Text:
                    item.SetText(representation.AsText());
                    break;
                case ExchangeFlavor.Html:
                    item.Set(DataFormat.CreateBytesPlatformFormat(names.HtmlFormat), names.EncodeHtml(representation.AsText()));
                    break;
                case ExchangeFlavor.Rtf:
                    item.Set(DataFormat.CreateBytesPlatformFormat(names.RtfFormat), names.EncodeRtf(representation.AsText()));
                    break;
                case ExchangeFlavor.Png:
                    using (var stream = new MemoryStream(representation.Data.ToArray()))
                        item.SetBitmap(new Avalonia.Media.Imaging.Bitmap(stream));
                    break;
                case ExchangeFlavor.Native:
                    item.Set(DataFormat.CreateBytesApplicationFormat(names.NativeFormat), representation.Data.ToArray());
                    break;
                case ExchangeFlavor.OpenDocument or ExchangeFlavor.OpenXml when representation.MediaType is { } mediaType:
                    item.Set(DataFormat.CreateBytesApplicationFormat(names.PackageFormat(mediaType)), representation.Data.ToArray());
                    break;
            }
        }
        var transfer = new DataTransfer();
        transfer.Add(item);
        return transfer;
    }

    public async Task<IReadOnlyList<ExchangeRepresentation>> ReadAsync(IAsyncDataTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        var names = formats.Current;
        var available = transfer.Formats;
        var result = new List<ExchangeRepresentation>();
        var native = DataFormat.CreateBytesApplicationFormat(names.NativeFormat);
        if (available.Contains(native) && await transfer.TryGetValueAsync(native).ConfigureAwait(true) is { Length: > 0 } nativeBytes)
            result.Add(new ExchangeRepresentation(ExchangeFlavor.Native, nativeBytes));
        foreach (var mediaType in _packages)
        {
            var package = DataFormat.CreateBytesApplicationFormat(names.PackageFormat(mediaType));
            if (available.Contains(package) && await transfer.TryGetValueAsync(package).ConfigureAwait(true) is { Length: > 0 } bytes)
                result.Add(new ExchangeRepresentation(Flavor(mediaType), bytes) { MediaType = mediaType });
        }
        var html = DataFormat.CreateBytesPlatformFormat(names.HtmlFormat);
        if (available.Contains(html) && await transfer.TryGetValueAsync(html).ConfigureAwait(true) is { } htmlBytes && names.DecodeHtml(htmlBytes) is { Length: > 0 } markup)
            result.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Html, markup));
        var rtf = DataFormat.CreateBytesPlatformFormat(names.RtfFormat);
        if (available.Contains(rtf) && await transfer.TryGetValueAsync(rtf).ConfigureAwait(true) is { } rtfBytes && names.DecodeRtf(rtfBytes) is { Length: > 0 } rich)
            result.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Rtf, rich));
        if (available.Contains(DataFormat.Text) && await transfer.TryGetTextAsync().ConfigureAwait(true) is { Length: > 0 } text)
            result.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Text, text));
        if (available.Contains(DataFormat.Bitmap) && await transfer.TryGetBitmapAsync().ConfigureAwait(true) is { } bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            result.Add(new ExchangeRepresentation(ExchangeFlavor.Png, stream.ToArray()));
        }
        return result;
    }

    private ExchangeFlavor Flavor(string mediaType) =>
        mediaType.StartsWith("application/vnd.oasis", StringComparison.Ordinal) ? ExchangeFlavor.OpenDocument : ExchangeFlavor.OpenXml;
}

public sealed class SystemClipboard(Func<IClipboard?> clipboard, IExchangeDataTransfers transfers) : ISystemClipboardText, IExchangeClipboard
{
    public bool IsAvailable => clipboard() is not null;

    public async Task<string?> GetTextAsync() =>
        clipboard() is { } system ? await system.TryGetTextAsync().ConfigureAwait(true) : null;

    public async Task SetTextAsync(string text)
    {
        if (clipboard() is { } system) await system.SetValueAsync(DataFormat.Text, text).ConfigureAwait(true);
    }

    public async Task WriteAsync(IReadOnlyList<ExchangeRepresentation> representations, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(representations);
        if (clipboard() is not { } system) return;
        await system.SetDataAsync(transfers.Write(representations)).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<ExchangeRepresentation>> ReadAsync(CancellationToken ct = default)
    {
        if (clipboard() is not { } system) return [];
        using var data = await system.TryGetDataAsync().ConfigureAwait(true);
        return data is null ? [] : await transfers.ReadAsync(data).ConfigureAwait(true);
    }
}
