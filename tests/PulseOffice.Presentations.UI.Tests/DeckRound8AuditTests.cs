using PulseOffice.Document.Commands;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using PulseOffice.Foundation.Zoom;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Macros;
using Avalonia.Controls;
using PulseOffice.Drawing.Assets;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound8AuditTests
{
    private static DeckTarget Target(Presentation deck)
    {
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());
        return new DeckTarget(deck, actions, () => deck.Slides.Count > 0 ? deck.Slides[0] : null, () => 0, () => { },
            _ => { }, () => false, () => null, () => [], _ => { }, _ => { }, () => null,
            () => Task.CompletedTask, _ => { }, () => DeckViewMode.Normal, () => null,
            () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => false, _ => false,
            new DeckViewOptions(), _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null);
    }

    private static Presentation Deck()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        return deck;
    }

    private static ObjectElement Control(string name, int z)
    {
        var control = new FormControlObject
        {
            Id = name,
            ControlKind = FormControlKind.PushButton,
            Properties = new FormControlCatalog().Create(FormControlKind.PushButton, name),
        };
        return new ObjectElement { Id = name, Bounds = new DocumentRect(new Twip(0), new Twip(z * 600), new Twip(1000), new Twip(500)), ZOrder = z, Content = control };
    }

    private sealed class SwitchingFormDialogs(Action during) : IFormControlDialogs
    {
        public Task<FormControlProperties?> EditAsync(FormControlObject control, MacroEventsContext events, string titleKey, CancellationToken ct) =>
            Task.FromResult<FormControlProperties?>(null);

        public Task<IReadOnlyList<string>?> TabOrderAsync(IReadOnlyList<FormControlProperties> controls, string titleKey, CancellationToken ct)
        {
            during();
            return Task.FromResult<IReadOnlyList<string>?>([.. controls.Select(control => control.Name).Reverse()]);
        }
    }

    [Fact]
    public async Task Tab_order_confirmed_after_switching_document_changes_the_document_it_was_opened_on()
    {
        var first = Deck();
        first.Slides[0].Add(Control("alpha", 0));
        first.Slides[0].Add(Control("beta", 1));
        var second = Deck();
        var context = new ActiveDeckContext();
        var firstTarget = Target(first);
        var secondTarget = Target(second);
        context.Attach(firstTarget);

        var commands = new DeckFormCommands(context, new SwitchingFormDialogs(() => context.Attach(secondTarget)), new ControlTabOrder(), null!);
        var command = commands.GetCommands().Single(each => each.Id == new CommandId("deck.form.tabOrder"));
        await command.Execute(null!, CancellationToken.None);

        Assert.True(firstTarget.Actions.History.CanUndo);
        Assert.False(secondTarget.Actions.History.CanUndo);
        Assert.Empty(second.Slides[0].Elements);
    }

    private sealed class HugeStream(long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => _position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, length - _position);
            _position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => _position = offset;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    private static ChosenFile File(string name, Func<Stream> open) => new(name, name)
    {
        OpenRead = _ => Task.FromResult(open()),
        OpenWrite = _ => Task.FromResult<Stream>(new MemoryStream()),
    };

    private sealed class Files(IReadOnlyList<ChosenFile> chosen, Action during) : IFileDialogService
    {
        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default)
        {
            during();
            return Task.FromResult<ChosenFile?>(chosen[0]);
        }

        public Task<IReadOnlyList<ChosenFile>> OpenFilesAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default)
        {
            during();
            return Task.FromResult(chosen);
        }

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<ChosenFile?>(null);
    }

    private sealed class Images(Action during) : IRasterImageFiles
    {
        public (int Width, int Height)? PixelSize(byte[] bytes) => (1, 1);
        public string MediaTypeOf(string fileName) => "image/png";
        public Task<RasterSource?> PickAsync(CancellationToken ct)
        {
            during();
            return Task.FromResult<RasterSource?>(new RasterSource([1, 2, 3], "image/png", 1, 1, "a.png"));
        }
        public Task<string?> SaveAsync(RasterImageObject raster, string suggestedName, CancellationToken ct) => Task.FromResult<string?>(null);
        public RasterImageObject Replace(RasterImageObject raster, RasterSource source) => raster;
        public CompressionResult Compress(RasterImageObject raster, int maximumEdge, int jpegQuality) => new(raster, 0, 0);
        public Task<CompressionResult?> CompressAsync(RasterImageObject raster, CancellationToken ct) => Task.FromResult<CompressionResult?>(null);
    }

    private sealed class Dialogs : IDialogService
    {
        public int Shown { get; private set; }
        public bool IsDialogOpen => false;
        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            Shown++;
            return Task.FromResult<TResult?>(default);
        }
        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default)
        {
            Shown++;
            return Task.CompletedTask;
        }
        public void Close(object? result) { }
    }

    private sealed class Status : IStatusBarService
    {
        public List<(string Message, NotificationSeverity Severity)> Reports { get; } = [];
        public IReadOnlyList<StatusSegment> Segments => [];
        public IReadOnlyList<BackgroundActivity> Activities => [];
        public event EventHandler? Changed { add { } remove { } }
        public StatusSegment Register(string id, StatusSegmentKind kind) => throw new NotSupportedException();
        public void Remove(string id) { }
        public void Report(string message, TimeSpan? duration = null) => Reports.Add((message, NotificationSeverity.Info));
        public void DismissTransient() { }
        public void Report(Notification notification) => Reports.Add((notification.Message, notification.Severity));
        public void Report(string message, NotificationSeverity severity, NotificationAction? action = null) => Reports.Add((message, severity));
        public BackgroundActivity BeginActivity(string description, CancellationTokenSource? cancellation = null) => throw new NotSupportedException();
        public void EndActivity(BackgroundActivity activity) { }
    }

    [Fact]
    public async Task Album_photos_beyond_the_image_limit_are_refused_without_reading_them()
    {
        var context = new ActiveDeckContext();
        context.Attach(Target(Deck()));
        var dialogs = new Dialogs();
        var status = new Status();
        var huge = File("huge.png", () => new HugeStream(200L * 1024 * 1024));
        var commands = new DeckExtraInsertCommands(context, null!, new Files([huge], () => { }), new Images(() => { }),
            dialogs, status, new FakeTextCatalog(), null!, new ChosenFileReader());

        await commands.GetCommands().Single(each => each.Id == new CommandId("deck.insert.photoAlbum")).Execute(null!, CancellationToken.None);

        Assert.Equal(0, dialogs.Shown);
        Assert.Contains(status.Reports, report => report.Severity == NotificationSeverity.Error && report.Message.Contains("huge.png", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_background_image_picked_while_its_slide_is_deleted_changes_nothing()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var context = new ActiveDeckContext();
        var target = Target(deck);
        context.Attach(target);
        Action remove = () => deck.Remove(slide);
        var png = File("a.png", () => new MemoryStream([1, 2, 3]));
        var commands = new DeckSlideCommands(context, new Files([png], remove), null!, null!, new FakeTextCatalog(), new Dialogs(), null!,
            new Images(remove), new ChosenFileReader(), new Status());

        await commands.GetCommands().Single(each => each.Id == new CommandId("deck.slide.backgroundImage")).Execute(null!, CancellationToken.None);

        Assert.Null(slide.BackgroundImage);
        Assert.False(target.Actions.History.CanUndo);
    }
}
