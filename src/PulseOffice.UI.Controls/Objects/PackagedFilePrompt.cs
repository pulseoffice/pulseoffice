using PulseOffice.Document.Embedding;
using PulseOffice.Localization;
using PulseOffice.Platform;

namespace PulseOffice.UI.Controls.Objects;

public interface IPackagedFilePrompt
{
    Task<PackagedObject?> PickAsync(PackagedObjectKind kind, string id, CancellationToken ct);
}

public sealed class PackagedFilePrompt(IFileDialogService files, ITextCatalog texts) : IPackagedFilePrompt
{
    public async Task<PackagedObject?> PickAsync(PackagedObjectKind kind, string id, CancellationToken ct)
    {
        var filter = kind == PackagedObjectKind.Media
            ? new FileFilter(texts["file.media"], ["*.mp3", "*.wav", "*.ogg", "*.m4a", "*.aac", "*.flac", "*.mp4", "*.mov", "*.m4v", "*.webm", "*.avi", "*.mkv"])
            : new FileFilter(texts["file.any"], ["*.*"]);
        var chosen = await files.OpenFileAsync(
            texts[kind == PackagedObjectKind.Media ? "dialog.insertMedia.title" : "dialog.insertObject.title"],
            [filter], ct).ConfigureAwait(true);
        if (chosen?.OpenRead is null) return null;
        using var buffer = new MemoryStream();
        await using (var stream = await chosen.OpenRead(ct).ConfigureAwait(true))
            await stream.CopyToAsync(buffer, ct).ConfigureAwait(true);
        var mediaType = MediaTypes.Of(chosen.Name);
        return new PackagedObject
        {
            Id = id,
            Packaged = kind,
            FileName = chosen.Name,
            MediaType = mediaType == "application/octet-stream" ? DocumentType(chosen.Name) : mediaType,
            Data = buffer.ToArray(),
        };
    }

    private string DocumentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        _ => "application/octet-stream",
    };
}
