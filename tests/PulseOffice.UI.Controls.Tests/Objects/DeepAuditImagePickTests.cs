using System.Reflection;
using PulseOffice.Drawing.Assets.Gif;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Objects;

public sealed class DeepAuditImagePickTests
{
    [Fact]
    public async Task Un_immagine_illeggibile_scelta_dal_disco_si_segnala_senza_eccezione()
    {
        var status = DispatchProxy.Create<IStatusBarService, RecordingProxy>();
        var files = new RasterImageFiles(new UnreadableFiles(), null!, new TextCatalog(UiLanguage.Italian), new BitmapPixels(), status, new PulseOffice.Drawing.Assets.RasterMediaTypes(), new GifCodec(), new ChosenFileReader());

        var picked = await files.PickAsync(CancellationToken.None);

        Assert.Null(picked);
        Assert.Contains(nameof(IStatusBarService.Report), ((RecordingProxy)(object)status).Calls);
    }

    private sealed class UnreadableFiles : IFileDialogService
    {
        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<ChosenFile?>(new ChosenFile("/x/foto.png", "foto.png")
            {
                OpenRead = _ => throw new UnauthorizedAccessException(),
                OpenWrite = _ => throw new UnauthorizedAccessException(),
            });

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<ChosenFile?>(null);
    }

    public class RecordingProxy : DispatchProxy
    {
        public List<string> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod!.Name);
            return targetMethod.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void) ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }
}
