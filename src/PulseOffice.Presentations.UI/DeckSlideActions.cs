using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckSlideActions(Presentation deck, ITextCatalog texts, DeckActionRecorder recorder, ISlideFactory slides, DeckShowActions shows)
{
    public Slide? AddSlide(SlideLayout layout, int afterIndex)
    {
        if (IsReadOnly) return null;
        var index = Math.Clamp(afterIndex + 1, 0, deck.Slides.Count);
        var neighbour = afterIndex >= 0 && afterIndex < deck.Slides.Count ? deck.Slides[afterIndex] : null;
        var master = neighbour is null ? deck.Master : deck.MasterOf(neighbour);
        var slide = slides.Create(deck.NextId(), master, layout);
        slide.MasterId = neighbour?.MasterId;

        Do(texts["deck.action.addSlide"],
            () => { deck.InsertExisting(index, slide); Focus(slide); },
            () => deck.Remove(slide));

        return slide;
    }

    public void DeleteSlide(Slide slide)
    {
        if (deck.Slides.Count <= 1) return;

        var index = deck.IndexOf(slide);
        if (index < 0) return;

        var showsBefore = deck.CustomShows.ToList();
        var showsAfter = showsBefore
            .Select(show => show with { SlideIds = [.. show.SlideIds.Where(id => id != slide.Id)] })
            .ToList();
        var setupBefore = deck.Setup;
        var remaining = deck.Slides.Count - 1;
        var setupAfter = setupBefore.WithoutSlide(index + 1, remaining);
        var next = index + 1 < deck.Slides.Count ? deck.Slides[index + 1] : null;
        var inherits = slide.SectionName is not null && next is { SectionName: null };
        var section = slide.SectionName;

        Do(texts["deck.action.deleteSlide"],
            () =>
            {
                deck.Remove(slide);
                ReplaceShows(showsAfter);
                deck.Setup = setupAfter;
                if (inherits) next!.SectionName = section;
                Focus(deck.Slides[Math.Clamp(index, 0, deck.Slides.Count - 1)]);
            },
            () =>
            {
                if (inherits) next!.SectionName = null;
                deck.Setup = setupBefore;
                ReplaceShows(showsBefore);
                deck.InsertExisting(index, slide);
                Focus(slide);
            });
    }

    public Slide? DuplicateSlide(Slide slide)
    {
        if (IsReadOnly) return null;
        var index = deck.IndexOf(slide);
        if (index < 0) return null;
        var copy = slides.Clone(slide, deck.NextId());

        Do(texts["deck.action.duplicateSlide"],
            () => { deck.InsertExisting(index + 1, copy); Focus(copy); },
            () => deck.Remove(copy));

        return copy;
    }

    public void ImportSlides(int afterIndex, IReadOnlyList<Slide> sources)
    {
        if (sources.Count == 0 || IsReadOnly) return;
        var clones = sources.Select(source => slides.Clone(source, deck.NextId())).ToList();
        foreach (var clone in clones) clone.MasterId = null;

        Do(texts["deck.action.importSlides"],
            () =>
            {
                for (var i = 0; i < clones.Count; i++)
                    deck.InsertExisting(Math.Clamp(afterIndex + 1 + i, 0, deck.Slides.Count), clones[i]);
                Focus(clones[^1]);
            },
            () => { foreach (var clone in clones) deck.Remove(clone); });
    }

    public IReadOnlyList<Slide> InsertOutlineSlides(int afterIndex, IReadOnlyList<OutlineSlide> outline, string labelKey)
    {
        if (outline.Count == 0 || IsReadOnly) return [];
        var near = Near(afterIndex);
        var created = outline.Select(entry => Build(entry, near)).ToList();

        Do(texts[labelKey],
            () =>
            {
                for (var i = 0; i < created.Count; i++)
                    deck.InsertExisting(Math.Clamp(afterIndex + 1 + i, 0, deck.Slides.Count), created[i]);
                Focus(created[0]);
            },
            () => { foreach (var slide in created) deck.Remove(slide); });

        return created;
    }

    private Slide? Near(int afterIndex) =>
        afterIndex >= 0 && afterIndex < deck.Slides.Count ? deck.Slides[afterIndex] : null;

    private Slide Created(Slide? neighbour, SlideLayout layout)
    {
        var slide = slides.Create(deck.NextId(), neighbour is null ? deck.Master : deck.MasterOf(neighbour), layout);
        slide.MasterId = neighbour?.MasterId;
        return slide;
    }

    private Slide Build(OutlineSlide entry, Slide? neighbour)
    {
        var slide = Created(neighbour, SlideLayout.TitleAndContent);
        if (slide.Placeholder(PlaceholderRole.Title) is { } title)
            slide.Replace(title with { Text = title.Text.WithPlainText(entry.Title) });

        if (slide.Placeholder(PlaceholderRole.Body) is { } body && entry.Body.Count > 0)
        {
            var seed = body.Text.Paragraphs.Count > 0 ? body.Text.Paragraphs[0] : new TextParagraph(string.Empty);
            slide.Replace(body with
            {
                Text = body.Text with
                {
                    Paragraphs = [.. entry.Body.Select(item => seed with
                    {
                        Text = item.Text,
                        Runs = item.Runs,
                        BulletLevel = item.BulletLevel,
                        Bullet = item.Bullet,
                        Numbered = item.Numbered,
                        NumberFormat = item.NumberFormat,
                    })],
                },
            });
        }

        return slide;
    }

    public int InsertAlbum(int afterIndex, IReadOnlyList<AlbumPage> pages, IReadOnlyList<PulseOffice.Drawing.Assets.RasterImageObject> photos, bool captions)
    {
        if (IsReadOnly) return 0;
        var created = new List<Slide>();
        var next = 0;
        var near = Near(afterIndex);
        foreach (var page in pages)
        {
            var slide = Created(near, captions ? SlideLayout.TitleOnly : SlideLayout.Blank);
            var names = new List<string>();
            foreach (var frame in page.Frames)
            {
                var photo = photos[next++];
                slide.Add(new ObjectElement { Id = photo.Id, Bounds = frame, ZOrder = slide.Elements.Count, Content = photo });
                if (photo.Name is { } name) names.Add(name);
            }
            if (captions && slide.Placeholder(PlaceholderRole.Title) is { } title)
                slide.Replace(title with { Text = title.Text.WithPlainText(string.Join(", ", names)) });
            created.Add(slide);
        }

        Do(texts["deck.action.photoAlbum"],
            () =>
            {
                for (var i = 0; i < created.Count; i++)
                    deck.InsertExisting(Math.Clamp(afterIndex + 1 + i, 0, deck.Slides.Count), created[i]);
                if (created.Count > 0) Focus(created[0]);
            },
            () => { foreach (var slide in created) deck.Remove(slide); });
        return created.Count;
    }

    public void MoveSlide(Slide slide, int to)
    {
        var from = deck.IndexOf(slide);
        if (from < 0) return;
        to = Math.Clamp(to, 0, deck.Slides.Count - 1);
        if (from == to) return;

        var markersBefore = deck.Slides.Select(s => (s, s.SectionName)).ToList();
        var markersAfter = new SlideSections().MarkersAfterMove(deck.Slides, from, to);

        Do(texts["deck.action.moveSlide"],
            () =>
            {
                deck.Move(deck.IndexOf(slide), to);
                for (var i = 0; i < deck.Slides.Count && i < markersAfter.Count; i++) deck.Slides[i].SectionName = markersAfter[i];
                Focus(slide);
            },
            () =>
            {
                deck.Move(deck.IndexOf(slide), from);
                foreach (var (s, name) in markersBefore) s.SectionName = name;
                Focus(slide);
            });
    }

    public void SetLayout(Slide slide, SlideLayout layout)
    {
        if (slide.Layout == layout) return;

        SetLayout(slide, deck.MasterOf(slide).Template(layout));
    }

    public void SetLayout(Slide slide, SlideLayoutTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var master = deck.MasterOf(slide);
        if (ReferenceEquals(master.Template(slide), template) || deck.IndexOf(slide) < 0) return;
        var replacement = slides.Relayout(slide, master, template);

        Do(texts["deck.action.layout"],
            () => { deck.ReplaceAt(deck.IndexOf(slide), replacement); Focus(replacement); },
            () => { deck.ReplaceAt(deck.IndexOf(replacement), slide); Focus(slide); });
    }

    public void SetSlideSize(SlideSize size, bool fitContents)
    {
        var beforeSize = deck.Size;
        var resize = new SlideResize();
        var beforeMasters = deck.Masters.ToList();
        var before = deck.Slides.SelectMany(slide => slide.Elements.Select(e => (slide, e))).ToList();
        var after = fitContents
            ? before.Select(pair => (pair.slide, e: pair.e with { Bounds = resize.Fit(pair.e.Bounds, beforeSize, size) })).ToList()
            : before;
        var afterMasters = fitContents ? beforeMasters.Select(master => resize.Fit(master, beforeSize, size)).ToList() : beforeMasters;

        Do(texts["deck.action.slideSize"],
            () =>
            {
                deck.Size = size;
                foreach (var master in afterMasters) deck.ReplaceMaster(master);
                foreach (var (slide, element) in after) slide.Replace(element);
                deck.Raise();
            },
            () =>
            {
                deck.Size = beforeSize;
                foreach (var master in beforeMasters) deck.ReplaceMaster(master);
                foreach (var (slide, element) in before) slide.Replace(element);
                deck.Raise();
            });
    }

    public void SetSection(Slide slide, string? name)
    {
        var before = slide.SectionName;
        if (before == name) return;

        Do(texts[name is null ? "deck.action.removeSection" : before is null ? "deck.action.addSection" : "deck.action.renameSection"],
            () => { slide.SectionName = name; deck.Raise(); },
            () => { slide.SectionName = before; deck.Raise(); });
    }

    public void SetBackgroundImage(Slide slide, PulseOffice.Drawing.Assets.RasterImageObject? image)
    {
        var before = (slide.BackgroundImage, slide.BackgroundFill);
        Do(texts["deck.action.background"],
            () => { slide.BackgroundImage = image; if (image is not null) slide.BackgroundFill = null; deck.Raise(); },
            () => { (slide.BackgroundImage, slide.BackgroundFill) = before; deck.Raise(); });
    }

    public void SetBackgroundFill(Slide slide, PulseOffice.Drawing.Shapes.ShapeFill? fill)
    {
        var before = (slide.BackgroundFill, slide.BackgroundImage);
        Do(texts["deck.action.background"],
            () => { slide.BackgroundFill = fill; if (fill is not null) slide.BackgroundImage = null; deck.Raise(); },
            () => { (slide.BackgroundFill, slide.BackgroundImage) = before; deck.Raise(); });
    }

    public void SetBackgrounds(IReadOnlyList<Slide> targets, SlideBackgroundChoice choice)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(choice);
        var before = targets.Select(slide => (slide, slide.BackgroundFill, slide.BackgroundImage, slide.BackgroundLayout, slide.ShowMasterObjects)).ToList();
        Do(texts["deck.action.background"],
            () =>
            {
                foreach (var slide in targets)
                {
                    slide.BackgroundFill = choice.Image is null ? choice.Fill : null;
                    slide.BackgroundImage = choice.Image;
                    slide.BackgroundLayout = choice.Layout;
                    slide.ShowMasterObjects = !choice.HideGraphics;
                }
                deck.Raise();
            },
            () =>
            {
                foreach (var (slide, fill, image, layout, objects) in before)
                {
                    slide.BackgroundFill = fill;
                    slide.BackgroundImage = image;
                    slide.BackgroundLayout = layout;
                    slide.ShowMasterObjects = objects;
                }
                deck.Raise();
            });
    }

    public void RenameSlide(Slide slide, string? name)
    {
        var before = slide.Name;
        if (before == name) return;

        Do(texts["deck.action.renameSlide"],
            () => { slide.Name = name; deck.Raise(); },
            () => { slide.Name = before; deck.Raise(); });
    }

    public void SetMasterDisplay(Slide slide, bool background, bool objects)
    {
        var (beforeBackground, beforeObjects) = (slide.ShowMasterBackground, slide.ShowMasterObjects);
        if (beforeBackground == background && beforeObjects == objects) return;

        Do(texts["deck.action.masterDisplay"],
            () => { slide.ShowMasterBackground = background; slide.ShowMasterObjects = objects; deck.Raise(); },
            () => { slide.ShowMasterBackground = beforeBackground; slide.ShowMasterObjects = beforeObjects; deck.Raise(); });
    }

    public void SetHidden(Slide slide, bool hidden)
    {
        if (slide.Hidden == hidden) return;

        Do(texts[hidden ? "deck.action.hideSlide" : "deck.action.showSlide"],
            () => { slide.Hidden = hidden; deck.Raise(); },
            () => { slide.Hidden = !hidden; deck.Raise(); });
    }

    private bool IsReadOnly => recorder.IsReadOnly;

    private void Do(string description, Action redo, Action undo) => recorder.Do(description, redo, undo);

    private void Focus(Slide slide) => recorder.Focus(slide);

    private void ReplaceShows(IReadOnlyList<CustomShow> list) => shows.ReplaceShows(list);
}
