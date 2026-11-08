using PulseOffice.Formats;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxMediaMapper
{
    private const string VideoRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/video";
    private const string AudioRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/audio";
    private const string MediaRelationship = "http://schemas.microsoft.com/office/2007/relationships/media";

    private readonly MediaTypes _types = new();

    private readonly DataReferenceRelationshipIndex _dataReferences = new();

    private readonly ExternalRelationshipIndex _externals = new();

    private readonly PptxMediaTiming _timing = new();


    private static readonly byte[] TransparentPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0B, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x60, 0x00, 0x02, 0x00,
        0x00, 0x05, 0x00, 0x01, 0x7A, 0x5E, 0xAB, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82,
    ];

    private static readonly byte[] DamagedTransparentPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x62, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    public P.Picture Build(uint id, MediaElement media, PresentationDocument document, SlidePart part)
    {
        var (referenceId, mediaReferenceId) = media.IsLinked ? Link(media, part) : Embed(media, document, part);

        var poster = part.AddImagePart(ImagePartType.Png);
        using (var target = poster.GetStream(FileMode.Create))
        {
            var bytes = media.PosterData ?? TransparentPng;
            target.Write(bytes, 0, bytes.Length);
        }

        var nvPr = new P.NonVisualDrawingProperties { Id = id, Name = media.FileName ?? media.Id };
        nvPr.Append(new D.HyperlinkOnClick { Id = string.Empty, Action = "ppaction://media" });

        var appNvPr = new P.ApplicationNonVisualDrawingProperties();
        if (media.Kind == MediaKind.Video)
            appNvPr.Append(new D.VideoFromFile { Link = referenceId });
        else
            appNvPr.Append(new D.AudioFromFile { Link = referenceId });
        appNvPr.Append(MediaExtension(mediaReferenceId, media.IsLinked));

        return new P.Picture(
            new P.NonVisualPictureProperties(
                nvPr,
                new P.NonVisualPictureDrawingProperties(new D.PictureLocks { NoChangeAspect = true }),
                appNvPr),
            new P.BlipFill(
                new D.Blip { Embed = part.GetIdOfPart(poster) },
                new D.Stretch(new D.FillRectangle())),
            new P.ShapeProperties(
                new D.Transform2D(
                    new D.Offset { X = media.Bounds.Left.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Y = media.Bounds.Top.Value * PulseOffice.Foundation.Units.Emu.PerTwip },
                    new D.Extents { Cx = media.Bounds.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Cy = media.Bounds.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip }),
                new D.PresetGeometry { Preset = D.ShapeTypeValues.Rectangle }));
    }

    public MediaElement? Read(P.Picture pic, SlidePart part, string id, int zOrder)
    {
        var appNvPr = pic.NonVisualPictureProperties?.ApplicationNonVisualDrawingProperties;
        if (appNvPr is null) return null;

        var video = appNvPr.GetFirstChild<D.VideoFromFile>();
        var audio = appNvPr.GetFirstChild<D.AudioFromFile>();
        var link = video?.Link?.Value ?? audio?.Link?.Value;
        if (link is null) return null;

        string? linked = null;
        byte[] data;
        string mime;
        if (_dataReferences.Of(part, link)?.DataPart is MediaDataPart dataPart)
        {
            using (var source = dataPart.GetStream())
            using (var buffer = new MemoryStream())
            {
                source.CopyTo(buffer);
                data = buffer.ToArray();
            }
            mime = _types.Resolve(dataPart.ContentType, dataPart.Uri.OriginalString, data);
        }
        else if (_externals.Of(part, link) is { } external)
        {
            linked = FromTarget(external.Uri);
            data = [];
            mime = _types.Resolve(null, linked, data);
        }
        else return null;

        var playback = pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Id?.Value is { } spid
            ? _timing.Read(part.Slide?.GetFirstChild<P.Timing>(), spid)
            : PptxMediaPlayback.Default;
        return new MediaElement
        {
            Id = id,
            Bounds = ReadBounds(pic),
            ZOrder = zOrder,
            Kind = video is not null && !_types.IsAudio(mime) ? MediaKind.Video : MediaKind.Audio,
            Data = data,
            MimeType = mime,
            FileName = pic.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value is { Length: > 0 } name && name != id ? name : null,
            PosterData = Poster(pic, part),
            LinkedPath = linked,
            Loop = playback.Loop,
            Muted = playback.Muted,
            Volume = playback.Volume,
        };
    }

    private (string Reference, string Media) Embed(MediaElement media, PresentationDocument document, SlidePart part)
    {
        var mediaPart = document.CreateMediaDataPart(media.MimeType, _types.Extension(media.MimeType));
        using (var target = mediaPart.GetStream(FileMode.Create))
            target.Write(media.Data, 0, media.Data.Length);

        var referenceId = media.Kind == MediaKind.Video
            ? part.AddVideoReferenceRelationship(mediaPart).Id
            : part.AddAudioReferenceRelationship(mediaPart).Id;
        return (referenceId, part.AddMediaReferenceRelationship(mediaPart).Id);
    }

    private (string Reference, string Media) Link(MediaElement media, SlidePart part)
    {
        var target = ToTarget(media.LinkedPath!);
        var reference = part.AddExternalRelationship(media.Kind == MediaKind.Video ? VideoRelationship : AudioRelationship, target);
        var mediaReference = part.AddExternalRelationship(MediaRelationship, target);
        return (reference.Id, mediaReference.Id);
    }

    private Uri ToTarget(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var absolute) ? absolute
            : Path.IsPathRooted(link) && Uri.TryCreate(Path.GetFullPath(link), UriKind.Absolute, out var rooted) ? rooted
            : Uri.TryCreate(link.Replace('\\', '/'), UriKind.Relative, out var relative) ? relative
            : new Uri(Uri.EscapeDataString(link), UriKind.Relative);

    private string FromTarget(Uri uri) => uri.OriginalString;

    private byte[]? Poster(P.Picture pic, SlidePart part)
    {
        if (pic.BlipFill?.Blip?.Embed?.Value is not { Length: > 0 } embed
            || !part.TryGetPartById(embed, out var found) || found is not ImagePart image)
            return null;
        using var source = image.GetStream();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        var bytes = buffer.ToArray();
        return bytes.AsSpan().SequenceEqual(TransparentPng) || bytes.AsSpan().SequenceEqual(DamagedTransparentPng) ? null : bytes;
    }

    private DocumentRect ReadBounds(P.Picture pic)
    {
        var xfrm = pic.ShapeProperties?.Transform2D;
        var offset = xfrm?.Offset;
        var extents = xfrm?.Extents;

        return new DocumentRect(
            new Twip((int)((offset?.X?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
            new Twip((int)((offset?.Y?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
            new Twip((int)((extents?.Cx?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
            new Twip((int)((extents?.Cy?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)));
    }

    private DocumentFormat.OpenXml.OpenXmlElement MediaExtension(string referenceId, bool linked)
    {
        var media = linked
            ? new DocumentFormat.OpenXml.Office2010.PowerPoint.Media { Link = referenceId }
            : new DocumentFormat.OpenXml.Office2010.PowerPoint.Media { Embed = referenceId };
        var ext = new P.ApplicationNonVisualDrawingPropertiesExtension { Uri = "{DAA4B4D4-6D71-4841-9C94-3DE7FCFB9230}" };
        ext.Append(media);
        var list = new P.ApplicationNonVisualDrawingPropertiesExtensionList();
        list.Append(ext);
        return list;
    }

}
