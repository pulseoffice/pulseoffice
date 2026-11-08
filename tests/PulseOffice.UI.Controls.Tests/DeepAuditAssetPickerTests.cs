using PulseOffice.Drawing.Assets;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditAssetPickerTests
{
    private sealed class SlowLibrary : IAssetLibrary
    {
        public Dictionary<string, TaskCompletionSource<IReadOnlyList<VectorAsset>>> Pending { get; } = [];

        public IReadOnlyCollection<AssetPack> Packs => [];

        public Task<IReadOnlyList<VectorAsset>> SearchAsync(string query, AssetPackKind? kind = null, string? packId = null, CancellationToken ct = default)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<VectorAsset>>();
            Pending[query] = completion;
            return completion.Task;
        }

        public Task<VectorAsset?> GetAsync(string packId, string itemId, CancellationToken ct = default) => Task.FromResult<VectorAsset?>(null);

        public Task<VectorAsset> ResolveGlyphAsync(GlyphOrigin origin, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static VectorAsset Asset(string id) => new() { Id = id, Name = id, SourceKind = VectorSourceKind.InlineSvg };

    [Fact]
    public Task LaRicercaNonBloccaIlThreadENonSovrascriveConRisultatiVecchi() => MacroEventsViewTests.Session.Dispatch(async () =>
    {
        var library = new SlowLibrary();
        var view = new AssetPickerView(library, new TextCatalog(UiLanguage.Italian));

        var older = view.ShowAsync("vecchia");
        var newer = view.ShowAsync("nuova");
        library.Pending["nuova"].SetResult([Asset("a")]);
        await newer;
        library.Pending["vecchia"].SetResult([Asset("b"), Asset("c")]);
        await older;
        library.Pending[string.Empty].SetResult([]);
        await view.Searched;

        Assert.Equal(1, view.ShownCount);
    }, CancellationToken.None);
}
