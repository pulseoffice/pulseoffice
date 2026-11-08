using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.CellFormat;

public sealed class CellAlignmentDialogView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly DialogFooter _footer;

    public CellAlignmentDialogView(CellStyle style, string sample, FormatCellsServices services, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        Page = new CellAlignmentPage(style, sample, services, texts, DesignTokens.For(InterfaceDensity.Compact));
        _footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Page.Style), primary: true);
        Content = Page;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public CellAlignmentPage Page { get; }
}
