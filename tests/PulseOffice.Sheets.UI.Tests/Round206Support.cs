using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.Tests;

internal sealed class Round206FixedDuplicatePrompt(DataDialogs.DuplicateRemovalChoice? choice = null) : DataDialogs.IDuplicateRemovalPrompt
{
    public int Calls { get; private set; }

    public Task<DataDialogs.DuplicateRemovalChoice?> AskAsync(Worksheet sheet, CellRange range, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult<DataDialogs.DuplicateRemovalChoice?>(choice
            ?? new DataDialogs.DuplicateRemovalChoice(false, [.. Enumerable.Range(range.Start.Column, range.ColumnCount)]));
    }
}

internal sealed class Round206FixedOverwritePrompt(bool answer) : ICellOverwritePrompt
{
    public int Calls { get; private set; }

    public Task<bool> ConfirmAsync(int cells, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(answer);
    }
}

internal sealed class Round206Status : IStatusBarService
{
    public List<string> Messages { get; } = [];
    public IReadOnlyList<StatusSegment> Segments => [];
    public IReadOnlyList<BackgroundActivity> Activities => [];
    public event EventHandler? Changed { add { } remove { } }
    public StatusSegment Register(string id, StatusSegmentKind kind) => new(id, kind);
    public void Remove(string id) { }
    public void Report(string message, TimeSpan? duration = null) => Messages.Add(message);
    public void DismissTransient() { }
    public void Report(Notification notification) => Messages.Add(notification.Message);
    public void Report(string message, NotificationSeverity severity, NotificationAction? action = null) => Messages.Add(message);
    public BackgroundActivity BeginActivity(string description, CancellationTokenSource? cancellation = null) => new("a", description, cancellation);
    public void EndActivity(BackgroundActivity activity) { }
}

internal sealed class Round206Scene
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    public static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    public Workbook Workbook { get; } = Workbook.CreateDefault();
    public UndoStack History { get; } = new();
    public ActiveSheetContext Context { get; } = new();
    public Engine.Recalculation.CalculationEngine Engine { get; }
    public CellRange Selection { get; set; } = CellRange.Single(CellAddress.First);
    public Worksheet Sheet => Workbook.Sheets[0];

    public Round206Scene()
    {
        Engine = Engines.Create(Workbook);
        Context.Attach(new SheetTarget
        {
            Workbook = Workbook,
            SheetIndex = 0,
            History = History,
            Engine = Engine,
            Selection = () => Selection,
            WriteSelection = range => Selection = range,
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => 1,
            WriteZoom = _ => { },
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
    }

    public static CellAddress A(string text) => CellAddress.TryParseA1(text, out var address) ? address : throw new ArgumentException(text);

    public static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    public string Text(string a1) => Sheet.GetValue(A(a1)) switch
    {
        { IsEmpty: true } => string.Empty,
        { Kind: ValueKind.Text } value => value.AsText(),
        var value => value.ToString(),
    };
}
