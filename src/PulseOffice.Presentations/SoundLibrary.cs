namespace PulseOffice.Presentations;

public sealed record EmbeddedSound(string Path, string MimeType, byte[] Data);

public sealed class SoundTypes
{
    private readonly IReadOnlyList<(string Extension, string MimeType)> _types =
        [("wav", "audio/wav"), ("wav", "audio/x-wav"), ("mp3", "audio/mpeg"), ("ogg", "audio/ogg"), ("m4a", "audio/mp4"), ("aif", "audio/aiff"), ("wma", "audio/x-ms-wma"), ("mid", "audio/midi")];

    public string MimeTypeOf(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return _types.Where(type => type.Extension == extension).Select(type => type.MimeType).DefaultIfEmpty("audio/wav").First();
    }

    public string ExtensionOf(string mimeType) =>
        _types.Where(type => string.Equals(type.MimeType, mimeType, StringComparison.OrdinalIgnoreCase)).Select(type => type.Extension).DefaultIfEmpty("wav").First();
}

public sealed class SoundLibrary
{
    private readonly Dictionary<string, EmbeddedSound> _sounds = new(StringComparer.Ordinal);

    public IReadOnlyCollection<EmbeddedSound> All => _sounds.Values;

    public void Add(EmbeddedSound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        _sounds[sound.Path] = sound;
    }

    public void Remove(string path) => _sounds.Remove(path);

    public EmbeddedSound? Find(string? path) => path is not null && _sounds.TryGetValue(path, out var sound) ? sound : null;

    public IEnumerable<string> Referenced(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return deck.Slides.SelectMany(slide => slide.Animations.Select(effect => effect.SoundPath)
                .Concat(slide.Interactions.Select(action => action.SoundPath))
                .Append(slide.Transition.SoundPath))
            .OfType<string>().Where(path => path.Length > 0).Distinct(StringComparer.Ordinal);
    }

    public string NextPath(string extension)
    {
        var number = _sounds.Count + 1;
        while (_sounds.ContainsKey($"Media/sound{number}.{extension}")) number++;
        return $"Media/sound{number}.{extension}";
    }
}
