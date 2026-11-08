using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Links;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.TextFormat;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ListPictureAndLinkTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.English);

    private readonly ListBulletPicture _picture = new(new byte[] { 1, 2, 3 }, "image/png", 12, 12);

    [Fact]
    public Task ThePictureIsSetAndRemovedInTheSharedListEditor() => Session.Dispatch(() =>
    {
        var editors = new ListLevelsEditors(new ListPresetCatalog(_texts), new ListSampleRows(new ListLabeler()), new GalleryNavigator(), new MeasureUnits(), null!, new TextFormatIcons(), _texts, null!);
        var request = new ListFormatRequest([new ListLevelFormat(ListNumbering.Bullet), new ListLevelFormat(ListNumbering.Bullet)], 1,
            [ListNumbering.None, ListNumbering.Bullet, ListNumbering.Arabic], ListFeatures.All);
        var editor = editors.Create(request, DesignTokens.For(InterfaceDensity.Compact));

        editor.SetPicture(_picture);

        Assert.Equal(_picture, editor.Answer.Levels[1].Picture);
        Assert.Null(editor.Answer.Levels[0].Picture);
        Assert.Equal(-1, editor.Gallery.SelectedIndex);

        editor.SetPicture(null);
        Assert.Null(editor.Answer.Levels[1].Picture);
    }, CancellationToken.None);

    [Fact]
    public void ChoosingACharacterPresetDropsThePicture()
    {
        var catalog = new ListPresetCatalog(_texts);
        var request = new ListFormatRequest([new ListLevelFormat(ListNumbering.Bullet) { Picture = _picture }], 0, [ListNumbering.Bullet], ListFeatures.All);
        var preset = catalog.Presets(request).First(item => item.Level.Numbering == ListNumbering.Bullet);

        var levels = catalog.Apply(request.Levels, 0, preset);

        Assert.Null(levels[0].Picture);
        Assert.Null(catalog.Matching(catalog.Presets(request), request.Levels[0], 0));
    }

    [Fact]
    public void TheSampleRowCarriesThePictureOfABulletLevel()
    {
        var rows = new ListSampleRows(new ListLabeler()).Rows([new ListLevelFormat(ListNumbering.Bullet) { Picture = _picture }], [0]);

        Assert.Equal(_picture, rows.Single().Picture);
    }

    [Fact]
    public void TheLinkFollowerOpensOnlyWhatTheShellAllowsAndRoutesPlaces()
    {
        var opener = new Opener();
        var follower = new LinkFollower(opener, new StatusBarService(TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System), new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System)), _texts);
        var places = new List<string>();

        Assert.True(follower.Follow("https://example.com", place => { places.Add(place); return true; }));
        Assert.False(follower.Follow("file:///etc/passwd", _ => true));
        Assert.True(follower.Follow("#Heading", place => { places.Add(place); return true; }));
        Assert.False(follower.Follow("  ", _ => true));

        Assert.Equal(["https://example.com"], opener.Opened);
        Assert.Equal(["#Heading"], places);
    }

    [Fact]
    public void NestedGroupMembersAreFoundAndRelinkedWithoutTouchingTheOthers()
    {
        var inner = new ShapeGroupObject
        {
            Id = "inner",
            Children = [new GroupChild(new ShapeObject { Id = "x", Shape = ShapeKind.Rectangle }, 0, 0, 1, 0.5), new GroupChild(new ShapeObject { Id = "y", Shape = ShapeKind.Rectangle }, 0, 0.5, 1, 0.5)],
        };
        var outer = new ShapeGroupObject
        {
            Id = "outer",
            Children = [new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.5, 1), new GroupChild(inner, 0.5, 0, 0.5, 1) { Hyperlink = "https://inner.example" }],
        };
        var links = new GroupChildLinks();

        var path = links.MemberAt(outer, 0.75, 0.8)!;

        Assert.Equal([1, 1], path.Indices);
        Assert.Null(links.Link(outer, path));
        Assert.Equal("https://inner.example", links.EffectiveLink(outer, path));
        var relinked = links.WithLink(outer, path, "https://y.example");
        Assert.Equal("https://y.example", ((ShapeGroupObject)relinked.Children[1].Content).Children[1].Hyperlink);
        Assert.Null(((ShapeGroupObject)relinked.Children[1].Content).Children[0].Hyperlink);
        Assert.Null(links.MemberAt(outer, double.NaN, 0.5));
        Assert.Same(outer, links.WithLink(outer, new GroupMemberPath([5]), "https://nowhere"));
    }

    private sealed class Opener : IShellOpener
    {
        public List<string> Opened { get; } = [];

        public OperationResult<bool> OpenUrl(string url)
        {
            if (!url.StartsWith("http", StringComparison.Ordinal)) return OperationResult<bool>.Fail(Diagnostic.Error("TEST", "blocked"));
            Opened.Add(url);
            return OperationResult<bool>.Ok(true);
        }

        public OperationResult<bool> OpenFile(string path) => OperationResult<bool>.Fail(Diagnostic.Error("TEST", "no"));
    }
}
