using PulseOffice.Document.Fonts;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;
using PulseOffice.Typography.Catalog;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.Testing;

public sealed class TestFontPickers
{
    public TestFontPickers(ITextCatalog texts, IDialogService dialogs, IReadOnlyList<FontFamilyInfo>? families = null)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(dialogs);
        var catalog = new FixedCatalog(families ?? []);
        var runtime = new RuntimeFonts(new FontFamilyMappingTable());
        Services = new FontPickerServices(
            catalog,
            new MemoryRecentFonts(),
            runtime,
            new IdleActivation(),
            dialogs,
            new FontDescriptions(texts),
            new IconCatalog(),
            new DiagnosticTexts(texts),
            new PlainDisplayFonts(),
            new FlyoutDismissal());
        Factory = new FontPickerFactory(Services);
    }

    public FontPickerServices Services { get; }

    public FontPickerFactory Factory { get; }

    private sealed class FixedCatalog(IReadOnlyList<FontFamilyInfo> families) : IFontCatalog
    {
        public event EventHandler? Changed { add { } remove { } }

        public Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default) => Task.FromResult(families);

        public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

        public FontFamilyInfo? Find(string family) => families.FirstOrDefault(info => string.Equals(info.Family, family, StringComparison.OrdinalIgnoreCase));

        public Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> AcquireAsync(FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok([]));
    }

    private sealed class MemoryRecentFonts : IRecentFonts
    {
        private readonly List<string> _families = [];

        public IReadOnlyList<string> All => _families;

        public void Remember(string family)
        {
            _families.Remove(family);
            _families.Insert(0, family);
        }
    }

    private sealed class IdleActivation : IFontActivation
    {
        public Task<OperationResult<bool>> ActivateAsync(FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<bool>.Ok(false));

        public void RegisterDocument(IFontDocument? document)
        {
        }

        public Task WarmUpAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class PlainDisplayFonts : IDisplayFonts
    {
        public Avalonia.Media.FontFamily Resolve(string? family) => Avalonia.Media.FontFamily.Default;

        public string? Effective(string? family) => family;
    }
}
