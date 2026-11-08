using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed record PptxSoundReference(string RelationshipId, string Name);

internal sealed class PptxSounds(SoundTypes types)
{
    public const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public string? Read(OpenXmlElement? sound, SlidePart slide, SoundLibrary library)
    {
        if (sound is null) return null;
        var embed = sound.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "embed" && attribute.NamespaceUri == Relationships).Value;
        if (string.IsNullOrEmpty(embed)
            || slide.DataPartReferenceRelationships.FirstOrDefault(relationship => relationship.Id == embed)?.DataPart is not { } part)
            return null;
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray();
        if (data.Length == 0) return null;

        var name = sound.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "name").Value;
        var extension = types.ExtensionOf(part.ContentType);
        var path = Named(name, extension);
        if (path is null || library.Find(path) is { } taken && !taken.Data.AsSpan().SequenceEqual(data))
            path = library.All.FirstOrDefault(existing => existing.Data.AsSpan().SequenceEqual(data))?.Path ?? library.NextPath(extension);
        if (library.Find(path) is null) library.Add(new EmbeddedSound(path, part.ContentType, data));
        return path;
    }

    public PptxSoundReference? Write(string? path, SlidePart slide, PptxWriteSession session)
    {
        if (session.Deck.Sounds.Find(path) is not { } sound) return null;
        if (!session.SoundParts.TryGetValue(sound.Path, out var part))
        {
            part = session.Document.CreateMediaDataPart(sound.MimeType, types.ExtensionOf(sound.MimeType));
            using (var stream = new MemoryStream(sound.Data)) part.FeedData(stream);
            session.SoundParts[sound.Path] = part;
        }
        var existing = slide.DataPartReferenceRelationships.FirstOrDefault(relationship => relationship.DataPart == part);
        return new PptxSoundReference(existing?.Id ?? slide.AddAudioReferenceRelationship(part).Id, Path.GetFileName(sound.Path));
    }

    public P.SoundAction Transition(PptxSoundReference sound, bool loop) =>
        new(new P.StartSoundAction(new P.Sound { Embed = sound.RelationshipId, Name = sound.Name }) { Loop = loop ? true : null });

    public D.HyperlinkSound Click(PptxSoundReference sound) => new() { Embed = sound.RelationshipId, Name = sound.Name };

    public bool Plays(string? path, PulseOffice.Presentations.Presentation deck) => deck.Sounds.Find(path) is not null;

    private string? Named(string? name, string extension)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var stem = new string([.. Path.GetFileNameWithoutExtension(name).Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')]);
        return stem.Length == 0 ? null : $"Media/{stem}.{extension}";
    }
}

internal sealed class PptxSoundReader(PptxSounds sounds, PptxTransitionMapper transitions) : IPptxSlidePartReader
{
    public void Read(PptxSlideSource source, SlideAssembly<uint> assembly)
    {
        var slide = assembly.Slide;
        var library = source.Sounds;
        if (transitions.Find(source.Slide.Slide)?.Descendants<P.StartSoundAction>().FirstOrDefault() is { } start
            && sounds.Read(start.GetFirstChild<P.Sound>(), source.Slide, library) is { } transitionSound)
            slide.Transition = slide.Transition with { SoundPath = transitionSound, LoopSoundUntilNext = start.Loop?.Value == true };

        foreach (var drawing in source.Tree?.Descendants<P.NonVisualDrawingProperties>() ?? [])
        {
            if (drawing.GetFirstChild<D.HyperlinkOnClick>()?.GetFirstChild<D.HyperlinkSound>() is not { } click
                || drawing.Id?.Value is not { } spid || !assembly.ElementByFrame.TryGetValue(spid, out var elementId)
                || sounds.Read(click, source.Slide, library) is not { } path)
                continue;
            slide.SetInteraction(slide.Interaction(elementId) is { } action && action.Kind != InteractionKind.None
                ? action with { SoundPath = path }
                : new InteractiveAction { TargetId = elementId, Kind = InteractionKind.PlaySound, SoundPath = path });
        }
    }
}
