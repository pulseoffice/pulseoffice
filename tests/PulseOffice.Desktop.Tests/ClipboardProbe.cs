using PulseOffice.Exchange;

namespace PulseOffice.Desktop.Tests;

public sealed record ClipboardContent
{
    public string? Text { get; init; }

    public string? Html { get; init; }

    public string? Rtf { get; init; }

    public byte[]? Png { get; init; }

    public byte[]? Native { get; init; }
}

public sealed class ClipboardProbe(IExchangeClipboard clipboard)
{
    public Task SetAsync(ClipboardContent content)
    {
        var representations = new List<ExchangeRepresentation>();
        if (content.Text is { } text) representations.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Text, text));
        if (content.Html is { } html) representations.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Html, html));
        if (content.Rtf is { } rtf) representations.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Rtf, rtf));
        if (content.Png is { } png) representations.Add(new ExchangeRepresentation(ExchangeFlavor.Png, png));
        return clipboard.WriteAsync(representations);
    }

    public async Task<ClipboardContent> GetAsync()
    {
        var representations = await clipboard.ReadAsync();
        return new ClipboardContent
        {
            Text = Find(representations, ExchangeFlavor.Text)?.AsText(),
            Html = Find(representations, ExchangeFlavor.Html)?.AsText(),
            Rtf = Find(representations, ExchangeFlavor.Rtf)?.AsText(),
            Png = Find(representations, ExchangeFlavor.Png)?.Data.ToArray(),
            Native = Find(representations, ExchangeFlavor.Native)?.Data.ToArray(),
        };
    }

    private ExchangeRepresentation? Find(IReadOnlyList<ExchangeRepresentation> representations, ExchangeFlavor flavor) =>
        representations.FirstOrDefault(representation => representation.Flavor == flavor);
}
