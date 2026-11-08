using System.Globalization;

namespace PulseOffice.Presentations;

public sealed record HeaderFooterSettings
{
    public bool ShowDate { get; init; }

    public bool DateIsFixed { get; init; }

    public string FixedDate { get; init; } = string.Empty;

    public HeaderFooterDateFormat DateFormat { get; init; } = HeaderFooterDateFormat.ShortDate;

    public bool ShowFooter { get; init; }

    public string FooterText { get; init; } = string.Empty;

    public bool ShowSlideNumber { get; init; }

    public bool ShowOnTitleSlide { get; init; }

    public bool Any => ShowDate || ShowFooter || ShowSlideNumber;
}

public readonly record struct SlideChrome(string? Date, string? Footer, string? Number)
{
    public bool IsEmpty => Date is null && Footer is null && Number is null;
}

public sealed class HeaderFooterResolver(IHeaderFooterDateFormats formats, PulseOffice.Text.Rich.ITextCultures cultures)
{
    public HeaderFooterResolver()
        : this(new HeaderFooterDateFormats(), new PulseOffice.Text.Rich.TextCultures())
    {
    }

    public SlideChrome Resolve(HeaderFooterSettings settings, Slide slide, int number, DateTime now, MasterElements? elements = null, string? language = null)
    {
        var culture = cultures.For(language);
        var shown = elements ?? MasterElements.All;
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(slide);

        if (!settings.Any || !slide.ShowMasterObjects) return default;
        if (slide.Layout == SlideLayout.TitleSlide && !settings.ShowOnTitleSlide) return default;

        var date = settings.ShowDate && shown.Date
            ? settings.DateIsFixed ? settings.FixedDate : formats.Render(settings.DateFormat, now, culture)
            : null;

        return new SlideChrome(
            string.IsNullOrEmpty(date) ? null : date,
            settings.ShowFooter && shown.Footer && !string.IsNullOrEmpty(settings.FooterText) ? settings.FooterText : null,
            settings.ShowSlideNumber && shown.SlideNumber ? number.ToString(culture) : null);
    }
}
