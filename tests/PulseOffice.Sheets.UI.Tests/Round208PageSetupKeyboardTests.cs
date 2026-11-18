using PulseOffice.Document.Pages;
using PulseOffice.Foundation.Pages;
using PulseOffice.Localization;
using PulseOffice.Sheets.UI.PageLayout;
using PulseOffice.Testing;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round208PageSetupKeyboardTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.English);

    [Fact]
    public Task The_page_setup_tabs_walk_with_tab_and_cancel_with_escape() => Session.Dispatch(() =>
    {
        var papers = new PaperCatalog();
        var input = new SheetPageSetupInput(new PageSetup { Paper = papers.Default }, papers.All, string.Empty, 0, 0, "Sheet1", "book.ods",
            System.Reflection.DispatchProxy.Create<IReferenceSource, LedgerCommandsAuditTests.Inert>());
        var view = new SheetPageSetupView(input, new SheetPageSetupServices(new PageSketchLayout(), new HeaderFooterTokens(), new MeasureUnits(), new IconCatalog()), _texts);
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            walk.WalkAndCancel(view.Build(), "dialog.pageSetup.title");
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);
}
