using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using PulseOffice.Document.Fonts;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;
using PulseOffice.Typography.Catalog;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditFontBrowserTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private sealed class UnreachableCatalog : IFontCatalog
    {
        public event EventHandler? Changed { add { } remove { } }

        public Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default) => throw new HttpRequestException("offline");

        public Task RefreshAsync(CancellationToken ct = default) => throw new HttpRequestException("offline");

        public FontFamilyInfo? Find(string family) => null;

        public Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> AcquireAsync(FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public Task Il_catalogo_dei_caratteri_irraggiungibile_non_fa_cadere_la_finestra() => Session.Dispatch(() =>
    {
        var services = new FontPickerServices(new UnreachableCatalog(), null!, null!, null!, null!,
            new FontDescriptions(new TextCatalog(UiLanguage.Italian)), new IconCatalog(), null!, null!, null!);
        var view = new FontBrowserView("Inter", services);
        var window = new Window { Width = 1100, Height = 700, Content = view };
        window.Show();

        Assert.True(view.Loading.IsCompletedSuccessfully);
        Assert.Equal(0, view.ShownCount);
        window.Close();
    }, CancellationToken.None);
}
