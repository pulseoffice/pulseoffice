using DocumentFormat.OpenXml;
using PulseOffice.Documents;
using PulseOffice.Documents.Protection;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxProtectionCodec
{
    public W.DocumentProtection? Write(TextDocument document)
    {
        var protection = document.Protection;
        var restriction = protection.IsActive ? protection.Restriction : document.Changes.ProtectionKey is not null ? DocumentRestriction.TrackedChanges : DocumentRestriction.None;
        if (restriction == DocumentRestriction.None) return null;
        var element = new W.DocumentProtection
        {
            Edit = restriction switch
            {
                DocumentRestriction.ReadOnly => W.DocumentProtectionValues.ReadOnly,
                DocumentRestriction.Comments => W.DocumentProtectionValues.Comments,
                DocumentRestriction.Forms => W.DocumentProtectionValues.Forms,
                _ => W.DocumentProtectionValues.TrackedChanges,
            },
            Enforcement = OnOffValue.FromBoolean(true),
        };
        if (protection.Key is { IsWord: true } key)
        {
            element.AlgorithmName = key.Algorithm;
            element.HashValue = Convert.ToBase64String(key.Hash);
            element.SaltValue = Convert.ToBase64String(key.Salt);
            element.SpinCount = key.SpinCount;
        }
        return element;
    }

    public void Read(W.DocumentProtection? element, TextDocument document)
    {
        if (element is null || !(element.Enforcement?.Value ?? false) || element.Edit?.Value is not { } edit) return;
        var restriction = edit == W.DocumentProtectionValues.ReadOnly ? DocumentRestriction.ReadOnly
            : edit == W.DocumentProtectionValues.Comments ? DocumentRestriction.Comments
            : edit == W.DocumentProtectionValues.Forms ? DocumentRestriction.Forms
            : edit == W.DocumentProtectionValues.TrackedChanges ? DocumentRestriction.TrackedChanges
            : DocumentRestriction.None;
        if (restriction == DocumentRestriction.None) return;
        ProtectionKey? key = null;
        if (element.AlgorithmName?.Value is { Length: > 0 } algorithm && element.HashValue?.Value is { Length: > 0 } hash)
        {
            try
            {
                key = new ProtectionKey(algorithm, Convert.FromBase64String(hash))
                {
                    Salt = element.SaltValue?.Value is { Length: > 0 } salt ? Convert.FromBase64String(salt) : [],
                    SpinCount = element.SpinCount?.Value is { } spin ? (int)Math.Min(spin, int.MaxValue) : 0,
                };
            }
            catch (FormatException)
            {
                key = null;
            }
        }
        document.Protection = new DocumentProtection(restriction, key);
        if (restriction == DocumentRestriction.TrackedChanges) document.Changes.IsRecording = true;
    }
}
