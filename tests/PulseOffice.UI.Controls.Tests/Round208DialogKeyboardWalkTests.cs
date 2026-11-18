using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using PulseOffice.Charts;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Tables;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round208DialogKeyboardWalkTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly TextCatalog _texts = new(UiLanguage.English);

    private Task Walk(Func<Control> build, string titleKey) => Session.Dispatch(() =>
    {
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            walk.WalkAndCancel(build(), titleKey);
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);

    private Task Confirm(Func<Control> build, string titleKey, Func<IInputElement?, bool>? from = null) => Session.Dispatch(() =>
    {
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            walk.ConfirmWithEnter(build(), titleKey, from);
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);

    private Control ChartEditor()
    {
        var mapper = new ChartSettingsMapper();
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = "Data", SourceRange = "A1:C3", ChartTitle = "Sales" };
        var data = new ChartData(["North", "South"], [new ChartSeries("Units", [4, 7])]);
        var request = new ChartPickerRequest
        {
            Mode = ChartPickerMode.Edit,
            Settings = mapper.From(chart),
            Culture = CultureInfo.InvariantCulture,
            Data = _ => data,
        };
        return new ChartPickerView(request, new ChartPickerServices(
            new ChartFamilyCatalog(), new ChartRecommender(), new ChartSampleData(_texts), new ChartRenderer(_texts), mapper, new IconCatalog(), _texts));
    }

    private Control TableLook() => new TableLookOptionsView(_texts, headerRow: true, TableBanding.Standard);

    private Control PdfExport() => new PdfExportView(new PdfExportDefaults("t", "a", "s", "k", 3),
        new PulseOffice.Printing.PageRangeParser(), _texts, new AvaloniaPageCanvasFactory(new PlainDisplayFonts()));

    private Control FindReplace() => new FindReplaceView(new FindTarget(), _texts);

    private Control MediaOptions() => new FormPromptView(
        _texts["deck.media.options.explanation"],
        [
            new CheckField("autoPlay", _texts["deck.media.options.autoPlay"], true),
            new CheckField("loop", _texts["deck.media.options.loop"]),
            new NumberField("volume", _texts["deck.media.options.volume"], 80, 0, 100, 1) { Unit = "%", Integer = true, ShowSlider = true },
            new NumberField("fadeIn", _texts["deck.media.options.fadeIn"], 0, 0, 86400, 0.25),
        ],
        _texts);

    [Fact]
    public Task The_chart_editor_tabs_walk_with_tab_and_cancel_with_escape() => Walk(ChartEditor, "dialog.chartEdit.title");

    [Fact]
    public Task The_table_look_options_walk_with_tab_and_cancel_with_escape() => Walk(TableLook, "command.deck.table.styleOptions.title");

    [Fact]
    public Task Enter_on_a_table_look_check_box_confirms_instead_of_toggling() => Confirm(TableLook, "command.deck.table.styleOptions.title",
        focused => focused is CheckBox);

    [Fact]
    public Task The_pdf_export_walks_with_tab_and_cancel_with_escape() => Walk(PdfExport, "dialog.exportPdf.title");

    [Fact]
    public Task Enter_in_the_pdf_export_runs_the_export() => Confirm(PdfExport, "dialog.exportPdf.title");

    [Fact]
    public Task Find_and_replace_walks_with_tab_and_cancel_with_escape() => Walk(FindReplace, "dialog.findReplace.title");

    [Fact]
    public Task Enter_in_the_find_field_finds_the_next_match() => Session.Dispatch(() =>
    {
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            var target = new FindTarget();
            var view = new FindReplaceView(target, _texts);
            _ = walk.Open(view, "dialog.findReplace.title");
            Assert.IsType<TextBox>(walk.Focused);

            walk.Press(Key.Enter, PhysicalKey.Enter);

            Assert.True(walk.IsOpen);
            Assert.Equal(1, target.Searches);
            Assert.Equal("found", view.Summary);
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task The_media_options_walk_with_tab_and_cancel_with_escape() => Walk(MediaOptions, "command.deck.object.mediaOptions.title");

    [Fact]
    public Task Enter_on_a_media_option_check_box_confirms() => Confirm(MediaOptions, "command.deck.object.mediaOptions.title",
        focused => focused is CheckBox);

    private sealed class FindTarget : IFindReplaceTarget
    {
        public int Searches { get; private set; }

        public IReadOnlyList<FindOption> Options { get; } = [new FindOption("case", "find.matchCase")];

        public string InitialQuery => "needle";

        public bool IsReadOnly => false;

        public bool SupportsFindNext => true;

        public string? Summarize(FindRequest request) => null;

        public string FindNext(FindRequest request)
        {
            Searches++;
            return "found";
        }

        public string ReplaceAll(FindRequest request) => "replaced";
    }

    private sealed class PlainDisplayFonts : PulseOffice.UI.Controls.Fonts.IDisplayFonts
    {
        public Avalonia.Media.FontFamily Resolve(string? family) => Avalonia.Media.FontFamily.Default;

        public string? Effective(string? family) => family;
    }
}
