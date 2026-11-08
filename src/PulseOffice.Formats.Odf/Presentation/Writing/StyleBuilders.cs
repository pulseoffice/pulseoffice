using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class PageStyleBuilder(OdfUnits units, SmilTransitionMapper transitions) : OdpXml
{
    public string Build(Slide slide, int number, OdpWriteSession session)
    {
        var properties = slide.BackgroundImage is { } image
            ? ImageFill(image, slide.BackgroundLayout, number, session)
            : new XElement(Style + "drawing-page-properties",
                new XAttribute(Draw + "fill", "solid"),
                new XAttribute(Draw + "fill-color", units.Hex(Background(session.Deck, slide))));
        if (slide.BackgroundImage is null && slide.BackgroundFill is { } fill)
        {
            properties.Attribute(Draw + "fill")?.Remove();
            properties.Attribute(Draw + "fill-color")?.Remove();
            session.ShapeStyles.WriteFill(properties, $"bgfill{number}", fill);
        }
        else if (slide.BackgroundImage is null && Background(session.Deck, slide) >> 24 is var alpha and < 255)
            properties.Add(new XAttribute(Draw + "opacity", $"{Math.Round(alpha / 2.55, 1).ToString(CultureInfo.InvariantCulture)}%"));

        Transition(properties, slide.Transition);
        if (slide.Hidden) properties.Add(new XAttribute(Presentation + "visibility", "hidden"));
        if (!slide.ShowMasterBackground) properties.Add(new XAttribute(Presentation + "background-visible", "false"));
        if (!slide.ShowMasterObjects) properties.Add(new XAttribute(Presentation + "background-objects-visible", "false"));
        if (session.Deck.HeaderFooter.Any) new OdfHeaderFooter().Display(properties, session.Deck.HeaderFooter, slide);

        var name = $"dp{number}";
        session.AddStyle(new XElement(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "drawing-page"),
            properties));
        return name;
    }

    private XElement ImageFill(PulseOffice.Drawing.Assets.RasterImageObject image, BackgroundImageLayout layout, int number, OdpWriteSession session)
    {
        var path = $"Pictures/bg_{number}.{units.Images.Extension(image.MediaType)}";
        session.AddMedia(path, image.Data.ToArray(), image.MediaType);

        var properties = new XElement(Style + "drawing-page-properties",
            new XAttribute(Draw + "fill", "bitmap"),
            new XAttribute(Draw + "fill-image-name", session.AddFillImage($"bgimg{number}", path)),
            new XAttribute(Style + "repeat", layout.Mode switch
            {
                BackgroundImageMode.Tile => "repeat",
                BackgroundImageMode.Natural => "no-repeat",
                _ => "stretch",
            }));
        if (layout.Width.Value > 0) properties.Add(new XAttribute(Draw + "fill-image-width", $"{Math.Round(layout.Width.ToCentimeters(), 3).ToString(CultureInfo.InvariantCulture)}cm"));
        if (layout.Height.Value > 0) properties.Add(new XAttribute(Draw + "fill-image-height", $"{Math.Round(layout.Height.ToCentimeters(), 3).ToString(CultureInfo.InvariantCulture)}cm"));
        if (layout.Opacity < 1) properties.Add(new XAttribute(Draw + "opacity", $"{Math.Round(layout.Opacity * 100, 1).ToString(CultureInfo.InvariantCulture)}%"));
        return properties;
    }

    private uint Background(PulseOffice.Presentations.Presentation deck, Slide slide) =>
        slide.Background ?? (slide.ShowMasterBackground ? deck.MasterOf(slide).BackgroundOf(deck.ThemeOf(slide), slide) : 0xFFFFFFFFu);

    private void Transition(XElement properties, SlideTransition transition)
    {
        if (transition.IsEmpty) return;

        if (!transition.IsNone)
        {
            var smil = transitions.ToSmil(transition);
            properties.Add(new XAttribute(Smil + "type", smil.Type));
            properties.Add(new XAttribute(Smil + "subtype", smil.Subtype));
            if (smil.Reverse) properties.Add(new XAttribute(Smil + "direction", "reverse"));
            properties.Add(new XAttribute(Smil + "dur", units.Seconds(transition.DurationMs)));
        }

        if (transition.Advance == AdvanceMode.AfterTime)
        {
            properties.Add(new XAttribute(Presentation + "transition-type", "automatic"));
            properties.Add(new XAttribute(Presentation + "duration", units.XmlDuration(transition.AdvanceAfterMs)));
        }
        else
        {
            properties.Add(new XAttribute(Presentation + "transition-type", "manual"));
        }

        if (!string.IsNullOrEmpty(transition.SoundPath))
            properties.Add(new XElement(Presentation + "sound",
                new XAttribute(Xlink + "href", transition.SoundPath),
                new XAttribute(Presentation + "play-full", transition.LoopSoundUntilNext ? "false" : "true")));
    }
}

internal sealed class SeedStyleBuilder(OdfUnits units, OdfTextFrameWriter frames) : OdpXml
{
    private const string Bullet = "sdBullet";

    public IEnumerable<XElement> Build(IReadOnlyList<(SlideMaster Master, Theme Theme, string Prefix)> masters)
    {
        var anyBullet = false;
        foreach (var (master, theme, prefix) in masters)
            foreach (var layout in master.Layouts.DistinctBy(layout => layout.Kind))
            {
                var seen = new Dictionary<PlaceholderRole, int>();
                foreach (var placeholder in layout.Placeholders)
                {
                    anyBullet |= placeholder.Seed.Bullet;
                    var occurrence = seen[placeholder.Role] = seen.GetValueOrDefault(placeholder.Role) + 1;
                    yield return SeedStyle(prefix, layout.Kind, placeholder, theme, occurrence);
                }
            }

        if (anyBullet)
            yield return new XElement(Text + "list-style",
                new XAttribute(Style + "name", Bullet),
                new XElement(Text + "list-level-style-bullet",
                    new XAttribute(Text + "level", "1"),
                    new XAttribute(Text + "bullet-char", "•")));
    }

    private XElement SeedStyle(string prefix, SlideLayout kind, PlaceholderTemplate placeholder, Theme theme, int occurrence)
    {
        var seed = placeholder.Seed;
        var layout = new XElement(Style + "paragraph-properties", new XAttribute(Fo + "text-align", units.Align(seed.Align)));
        if (seed.Bullet) layout.Add(new XAttribute(Style + "list-style-name", Bullet));
        layout.Add(frames.Indents(seed));

        return new XElement(Style + "style",
            new XAttribute(Style + "name", new OdfSeedNames().Of(prefix, kind, placeholder.Role, occurrence)),
            new XAttribute(Style + "family", "presentation"),
            new XElement(Style + "graphic-properties", frames.Attributes(placeholder.Frame, placeholder.Bounds.Height)),
            layout,
            new XElement(Style + "text-properties",
                new XAttribute(Fo + "font-size", units.Pt(seed.FontSize)),
                new XAttribute(Fo + "font-weight", seed.Bold ? "bold" : "normal"),
                new XAttribute(Fo + "font-style", seed.Italic ? "italic" : "normal"),
                new XAttribute(Fo + "font-family", seed.MajorFont ? theme.Fonts.Major : theme.Fonts.Minor),
                new XAttribute(Fo + "color", units.Hex(seed.Color ?? theme.Colors.Resolve(seed.ColorRole ?? ColorRole.Text)))));
    }
}
