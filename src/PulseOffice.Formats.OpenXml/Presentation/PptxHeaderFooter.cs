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

    public IEnumerable<P.Shape> Shapes(HeaderFooterSettings settings, Slide slide, int number, SlideSize size, uint firstId, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Any || !slide.ShowMasterObjects) yield break;
        if (slide.Layout == SlideLayout.TitleSlide && !settings.ShowOnTitleSlide) yield break;

        var width = size.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip;
        var height = size.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip;
        var id = firstId;

        if (settings.ShowDate)
            yield return Shape(id++, "Date Placeholder", P.PlaceholderValues.DateAndTime, 10, width, height, 0.04, settings.DateIsFixed
                ? Run(settings.FixedDate)
                : Field(_formats.For(settings.DateFormat).PptxType,
                    _formats.Render(settings.DateFormat, DateTime.Now, cultures.For(language))));

        if (settings.ShowFooter)
            yield return Shape(id++, "Footer Placeholder", P.PlaceholderValues.Footer, 11, width, height, 0.36, Run(settings.FooterText));

        if (settings.ShowSlideNumber)
            yield return Shape(id, "Slide Number Placeholder", P.PlaceholderValues.SlideNumber, 12, width, height, 0.72,
                Field("slidenum", number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public HeaderFooterSettings? Read(IEnumerable<SlidePart> slides)
    {
        var placeholders = slides
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
            ShowOnTitleSlide = true,
        };
        return settings.Any ? settings : null;
    }

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
