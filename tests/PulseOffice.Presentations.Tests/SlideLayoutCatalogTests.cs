using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideLayoutCatalogTests
{
    private readonly ISlideLayoutCatalog _catalog = new SlideLayoutCatalog();
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new Presentation().Theme);

    [Fact]
    public void Duplicate_inserts_a_custom_copy_after_the_source_with_a_fresh_identity()
    {
        var standard = _master.Template(SlideLayout.TitleAndContent);
        var first = _catalog.Duplicate(_master, standard.Id, "Agenda");
        var second = _catalog.Duplicate(first.Master, standard.Id, " ");

        Assert.Equal("layout.custom.1", first.Copy.Id);
        Assert.Equal("Agenda", first.Copy.Name);
        Assert.Equal("layout.custom.2", second.Copy.Id);
        Assert.Equal(standard.Name, second.Copy.Name);
        var layouts = second.Master.Layouts.ToList();
        Assert.Equal(layouts.IndexOf(standard) + 1, layouts.IndexOf(second.Copy));
        Assert.True(_catalog.IsStandard(second.Master, standard));
        Assert.False(_catalog.IsStandard(second.Master, second.Copy));
    }

    [Fact]
    public void Only_custom_layouts_can_be_renamed_or_removed()
    {
        var standard = _master.Template(SlideLayout.TitleOnly);
        var copy = _catalog.Duplicate(_master, standard.Id, "Mine");

        Assert.Same(copy.Master, _catalog.Rename(copy.Master, standard.Id, "Other"));
        Assert.Same(copy.Master, _catalog.Remove(copy.Master, standard.Id));
        Assert.Same(copy.Master, _catalog.Rename(copy.Master, copy.Copy.Id, "   "));
        Assert.Equal("Yours", _catalog.Find(_catalog.Rename(copy.Master, copy.Copy.Id, " Yours "), copy.Copy.Id)!.Name);
        Assert.Null(_catalog.Find(_catalog.Remove(copy.Master, copy.Copy.Id), copy.Copy.Id));
        Assert.Throws<ArgumentException>(() => _catalog.Duplicate(_master, "missing", "x"));
    }

    [Fact]
    public void Editing_a_layout_by_type_no_longer_touches_its_custom_siblings()
    {
        var standard = _master.Template(SlideLayout.TitleAndContent);
        var copy = _catalog.Duplicate(_master, standard.Id, "Mine");
        var bounds = new DocumentRect(new Twip(10), new Twip(10), new Twip(500), new Twip(500));

        var edited = new MasterEditing().WithPlaceholderBounds(copy.Master, SlideLayout.TitleAndContent, PlaceholderRole.Title, bounds);
        var customEdited = new MasterEditing().WithPlaceholderBounds(copy.Master, copy.Copy.Id, PlaceholderRole.Title, bounds);

        Assert.Equal(bounds, edited.Template(SlideLayout.TitleAndContent).Placeholders.First(p => p.Role == PlaceholderRole.Title).Bounds);
        Assert.NotEqual(bounds, _catalog.Find(edited, copy.Copy.Id)!.Placeholders.First(p => p.Role == PlaceholderRole.Title).Bounds);
        Assert.Equal(bounds, _catalog.Find(customEdited, copy.Copy.Id)!.Placeholders.First(p => p.Role == PlaceholderRole.Title).Bounds);
        Assert.NotEqual(bounds, customEdited.Template(SlideLayout.TitleAndContent).Placeholders.First(p => p.Role == PlaceholderRole.Title).Bounds);
    }
}
