using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxHeaderFooter(IHeaderFooterDateFormats formats, PulseOffice.Text.Rich.ITextCultures cultures)
{
    public PptxHeaderFooter()
        : this(new HeaderFooterDateFormats(), new PulseOffice.Text.Rich.TextCultures())
    {
    }

    private readonly IHeaderFooterDateFormats _formats = formats;

    public IEnumerable<P.Shape> Shapes(HeaderFooterSettings settings, Slide slide, int number, SlideSize size, uint firstId, string? language = null, MasterElements? elements = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var shown = elements ?? MasterElements.All;
        if (!settings.Any || !slide.ShowMasterObjects) yield break;
        if (slide.Layout == SlideLayout.TitleSlide && !settings.ShowOnTitleSlide) yield break;

        var width = size.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip;
        var height = size.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip;
        var id = firstId;

        if (settings.ShowDate && shown.Date)
            yield return Shape(id++, "Date Placeholder", P.PlaceholderValues.DateAndTime, 10, width, height, 0.04, settings.DateIsFixed
                ? Run(settings.FixedDate)
                : Field(_formats.For(settings.DateFormat).PptxType,
                    _formats.Render(settings.DateFormat, DateTime.Now, cultures.For(language))));

        if (settings.ShowFooter && shown.Footer)
            yield return Shape(id++, "Footer Placeholder", P.PlaceholderValues.Footer, 11, width, height, 0.36, Run(settings.FooterText));

        if (settings.ShowSlideNumber && shown.SlideNumber)
            yield return Shape(id, "Slide Number Placeholder", P.PlaceholderValues.SlideNumber, 12, width, height, 0.72,
                Field("slidenum", number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public HeaderFooterSettings? Read(IEnumerable<SlidePart> slides)
    {
        var parts = slides.ToList();
        var placeholders = parts
            .SelectMany(part => part.Slide?.CommonSlideData?.ShapeTree?.Elements<P.Shape>() ?? [])
            .Select(shape => (Shape: shape, Type: shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.GetFirstChild<P.PlaceholderShape>()?.Type?.Value))
            .Where(item => item.Type is not null)
            .ToList();

        var date = placeholders.FirstOrDefault(item => item.Type == P.PlaceholderValues.DateAndTime).Shape;
        var footer = placeholders.FirstOrDefault(item => item.Type == P.PlaceholderValues.Footer).Shape;
        var number = placeholders.Any(item => item.Type == P.PlaceholderValues.SlideNumber);
        var dateField = date?.Descendants<D.Field>().FirstOrDefault();
        var fixedDate = date is not null && dateField is null;

        var settings = new HeaderFooterSettings
        {
            ShowDate = date is not null,
            DateIsFixed = fixedDate,
            FixedDate = fixedDate ? date!.InnerText : string.Empty,
            DateFormat = _formats.FromPptxType(dateField?.Type?.Value) ?? HeaderFooterDateFormat.ShortDate,
            ShowFooter = footer is not null,
            FooterText = footer?.InnerText ?? string.Empty,
            ShowSlideNumber = number,
            ShowOnTitleSlide = ShownOnTitleSlides(parts),
        };
        return settings.Any ? settings : null;
    }

    private bool ShownOnTitleSlides(IReadOnlyList<SlidePart> parts)
    {
        var titles = parts.Where(IsTitleSlide).ToList();
        if (titles.Count == 0) return true;
        if (titles.Count == parts.Count) return true;
        return titles.Any(HasChrome);
    }

    private bool IsTitleSlide(SlidePart part) =>
        part.SlideLayoutPart?.SlideLayout?.Type?.Value == P.SlideLayoutValues.Title;

    private bool HasChrome(SlidePart part) =>
        (part.Slide?.CommonSlideData?.ShapeTree?.Elements<P.Shape>() ?? [])
            .Select(shape => shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.GetFirstChild<P.PlaceholderShape>()?.Type?.Value)
            .Any(type => type == P.PlaceholderValues.DateAndTime || type == P.PlaceholderValues.Footer || type == P.PlaceholderValues.SlideNumber);

    private P.Shape Shape(uint id, string name, P.PlaceholderValues type, uint index, long width, long height, double left, D.Paragraph paragraph) =>
        new(new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = type, Size = P.PlaceholderSizeValues.Quarter, Index = index })),
            new P.ShapeProperties(new D.Transform2D(
                new D.Offset { X = (long)(left * width), Y = (long)(height * 0.925) },
                new D.Extents { Cx = (long)(width * 0.24), Cy = (long)(height * 0.053) })),
            new P.TextBody(new D.BodyProperties(), new D.ListStyle(), paragraph));

    private D.Paragraph Run(string text) =>
        new(new D.Run(new D.RunProperties(), new D.Text(text)));

    private D.Paragraph Field(string type, string text) =>
        new(new D.Field(new D.RunProperties(), new D.Text(text)) { Id = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}", Type = type });
}
