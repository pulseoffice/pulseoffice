using System.Globalization;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Security;

public interface IPdfSigner
{
    bool IsPdf(string path);

    void Sign(string path, X509Certificate2 certificate, string? reason = null, string? place = null, PdfSignatureAppearance? appearance = null);

    IReadOnlyList<DocumentSignature> Read(string path);
}

public sealed class PdfSigner : IPdfSigner
{
    public PdfSigner() : this(new CertificateJudge())
    {
    }

    public PdfSigner(CertificateJudge judge) : this(judge, new StandardPdfSignatureFont())
    {
    }

    public PdfSigner(CertificateJudge judge, IPdfSignatureFont font)
    {
        _signature = new DetachedSignature(judge);
        _widget = new PdfSignatureWidget(font);
    }

    private readonly PdfTrailer _trailer = new();
    private readonly PdfCatalogue _catalogue = new();
    private readonly PdfSignatureWidget _widget;
    private readonly PdfSignatureSlot _slot = new();
    private readonly DetachedSignature _signature;
    private readonly PdfLiteral _literal = new();
    private readonly SignatureStrength _strength = new();

    public bool IsPdf(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path)) return false;

        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[5];

        return stream.Read(header) == 5 && Encoding.ASCII.GetString(header) == "%PDF-";
    }

    public void Sign(string path, X509Certificate2 certificate, string? reason = null, string? place = null, PdfSignatureAppearance? appearance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(certificate);

        var original = File.ReadAllBytes(path);
        var text = Encoding.Latin1.GetString(original);

        var trailer = _trailer.Read(text);
        var index = new PdfObjectIndex(text);
        var catalogue = index.Catalogue(trailer.Root) ?? throw new InvalidOperationException("The file has no catalogue.");

        var signatureId = trailer.Size;
        var fieldId = trailer.Size + 1;
        var signer = SignerIdentity.From(certificate).Name;
        var now = DateTimeOffset.Now;
        var update = new PdfIncrementalUpdate(original);
        var widget = appearance is null
            ? _widget.Invisible()
            : _widget.Visible(index, catalogue, fieldId, new AppendedObjects(update, trailer.Size + 2), appearance, signer, reason, place, now);

        update.Add(signatureId, _slot.Dictionary(signer, reason, place, now, _literal));
        update.Add(fieldId,
            $"<< /Type /Annot /Subtype /Widget /FT /Sig /T (PulseOfficeSignature{fieldId}) /V {signatureId} 0 R {widget.Entries} >>");
        foreach (var extra in widget.Objects) update.Add(extra.Id, extra.Body);
        update.Add(catalogue.Id, _catalogue.WithForm(catalogue.Body, fieldId));

        var bytes = update.Finish(catalogue.Id, trailer);
        var range = _slot.Seal(bytes, original.Length);
        _slot.Fill(bytes, _signature.Sign(range.Content(bytes), certificate), original.Length);

        var temporary = path + ".signing";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    public IReadOnlyList<DocumentSignature> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path)) return [];

        var bytes = File.ReadAllBytes(path);
        var text = Encoding.Latin1.GetString(bytes);

        if (!_slot.HasDetachedSignature(text)) return [];

        var verified = _slot.Slots(text)
            .Where(slot => slot.Range.Fits(bytes))
            .Select(slot => (slot, encoded: _slot.Signature(bytes, slot.Range)))
            .Where(pair => pair.encoded is not null)
            .Select(pair => (pair.slot, result: _signature.Verify(pair.slot.Range.Content(bytes), pair.encoded!)))
            .Where(pair => pair.result is not null)
            .ToList();

        var finalRevision = _strength.Strongest(verified
            .Where(pair => pair.slot.Range.CoversWhole(bytes))
            .Select(pair => pair.result!.Status));

        return [.. verified.Select(pair => new DocumentSignature(
            pair.result!.Signer,
            pair.slot.Date,
            pair.slot.Range.CoversWhole(bytes) ? pair.result.Status : _strength.Weakest(pair.result.Status, finalRevision),
            ["*"]))];
    }
}

internal sealed class SignatureStrength
{
    private readonly SignatureStatus[] _order =
    [
        SignatureStatus.Valid,
        SignatureStatus.UnverifiableInTime,
        SignatureStatus.ExpiredCertificate,
        SignatureStatus.UntrustedCertificate,
        SignatureStatus.Unknown,
        SignatureStatus.Broken,
    ];

    public SignatureStatus Strongest(IEnumerable<SignatureStatus> statuses) =>
        _order[statuses.Select(Rank).DefaultIfEmpty(_order.Length - 1).Min()];

    public SignatureStatus Weakest(SignatureStatus first, SignatureStatus second) => _order[Math.Max(Rank(first), Rank(second))];

    private int Rank(SignatureStatus status) => Array.IndexOf(_order, status);
}

internal sealed class PdfLiteral
{
    public string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}

internal sealed record PdfByteRange(long[] Values)
{
    public bool Fits(byte[] bytes) =>
        Values[0] == 0 && Values[1] > 0 && Values[2] > Values[1] && Values[3] >= 0
        && Values[2] + Values[3] <= bytes.Length
        && bytes[Values[1]] == (byte)'<' && bytes[Values[2] - 1] == (byte)'>';

    public bool CoversWhole(byte[] bytes) => Values[2] + Values[3] == bytes.Length;

    public byte[] Content(byte[] bytes)
    {
        var content = new byte[Values[1] + Values[3]];
        Array.Copy(bytes, Values[0], content, 0, Values[1]);
        Array.Copy(bytes, Values[2], content, Values[1], Values[3]);
        return content;
    }
}

internal sealed record PdfSlot(PdfByteRange Range, DateTimeOffset Date);

internal sealed partial class PdfSignatureSlot
{
    [GeneratedRegex(@"/SubFilter\s*/(adbe\.pkcs7\.detached|ETSI\.CAdES\.detached)")]
    private partial Regex DetachedSubFilter();

    [GeneratedRegex(@"/ByteRange\s*\[([^\]]*)\]")]
    private partial Regex ByteRangeEntry();

    [GeneratedRegex(@"^(\d{4})(\d{2})?(\d{2})?(\d{2})?(\d{2})?(\d{2})?(?:(Z)|([+-])(\d{2})'?(\d{2})?'?)?")]
    private partial Regex PdfDate();

    private const int ReservedBytes = 8192;
    private const string ContentsMarker = "/Contents <";
    private const string RangeMarker = "/ByteRange [";

    private readonly DerEnvelope _der = new();

    public string Dictionary(string signer, string? reason, string? place, DateTimeOffset when, PdfLiteral literal)
    {
        var text = new StringBuilder("<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached\n");
        text.Append(CultureInfo.InvariantCulture, $"/Name ({literal.Escape(signer)})\n");
        text.Append(CultureInfo.InvariantCulture, $"/M (D:{when:yyyyMMddHHmmss}{Offset(when)})\n");
        if (reason is { Length: > 0 }) text.Append(CultureInfo.InvariantCulture, $"/Reason ({literal.Escape(reason)})\n");
        if (place is { Length: > 0 }) text.Append(CultureInfo.InvariantCulture, $"/Location ({literal.Escape(place)})\n");
        text.Append(RangeMarker).Append("0 @@@@@@@@@@ @@@@@@@@@@ @@@@@@@@@@]\n");
        text.Append(ContentsMarker).Append(new string('0', ReservedBytes * 2)).Append(">\n>>");
        return text.ToString();
    }

    public PdfByteRange Seal(byte[] bytes, int from)
    {
        var contentsStart = Find(bytes, ContentsMarker, from) + ContentsMarker.Length;
        var contentsEnd = contentsStart + ReservedBytes * 2;
        var range = new PdfByteRange([0, contentsStart - 1, contentsEnd + 1, bytes.Length - contentsEnd - 1]);

        var start = Find(bytes, RangeMarker, from);
        if (start < 0) throw new InvalidOperationException("The signature has no byte range.");

        var close = Array.IndexOf(bytes, (byte)']', start);
        var text = string.Create(CultureInfo.InvariantCulture, $"{RangeMarker}{string.Join(' ', range.Values)}]");
        if (text.Length > close - start + 1) throw new InvalidOperationException("The byte range does not fit.");

        Write(bytes, start, text.PadRight(close - start + 1, ' '));
        return range;
    }

    public void Fill(byte[] bytes, byte[] signature, int from)
    {
        var hex = Convert.ToHexString(signature);
        if (hex.Length > ReservedBytes * 2) throw new InvalidOperationException("The signature does not fit in the reserved space.");

        Write(bytes, Find(bytes, ContentsMarker, from) + ContentsMarker.Length, hex);
    }

    public byte[]? Signature(byte[] bytes, PdfByteRange range)
    {
        var start = (int)range.Values[1] + 1;
        var end = (int)range.Values[2] - 1;

        try
        {
            var padded = Convert.FromHexString(Encoding.ASCII.GetString(bytes, start, end - start));
            return _der.Length(padded) is { } length && length <= padded.Length ? padded[..length] : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public bool HasDetachedSignature(string text) => DetachedSubFilter().IsMatch(text);

    public IEnumerable<PdfSlot> Slots(string text)
    {
        foreach (Match entry in ByteRangeEntry().Matches(text))
            if (Range(entry.Groups[1].Value) is { } range) yield return new PdfSlot(range, Date(text, entry.Index));
    }

    private PdfByteRange? Range(string listed)
    {
        var parts = listed.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var values = new long[4];

        return parts.Length == 4 && parts.Select((part, i) =>
                long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i])).All(ok => ok)
            ? new PdfByteRange(values)
            : null;
    }

    private DateTimeOffset Date(string text, int rangeMarker)
    {
        var dictionary = text.LastIndexOf(" obj", rangeMarker, StringComparison.Ordinal);
        var ending = text.IndexOf("endobj", rangeMarker, StringComparison.Ordinal);
        if (dictionary < 0 || ending < 0) return DateTimeOffset.MinValue;
        var marker = text.IndexOf("/M (D:", dictionary, ending - dictionary, StringComparison.Ordinal);
        if (marker < 0) return DateTimeOffset.MinValue;

        return Parse(text.Substring(marker + 6, Math.Min(32, ending - marker - 6)));
    }

    private DateTimeOffset Parse(string value)
    {
        var match = PdfDate().Match(value);
        if (!match.Success) return DateTimeOffset.MinValue;

        int Part(int group, int fallback) =>
            match.Groups[group].Success ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) : fallback;

        try
        {
            var offset = match.Groups[8].Success
                ? new TimeSpan(Part(9, 0), Part(10, 0), 0) * (match.Groups[8].Value == "-" ? -1 : 1)
                : match.Groups[7].Success ? TimeSpan.Zero : TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            return new DateTimeOffset(Part(1, 1), Part(2, 1), Part(3, 1), Part(4, 0), Part(5, 0), Part(6, 0), offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.MinValue;
        }
    }

    private string Offset(DateTimeOffset when)
    {
        var offset = when.Offset;
        return string.Create(CultureInfo.InvariantCulture,
            $"{(offset < TimeSpan.Zero ? '-' : '+')}{Math.Abs(offset.Hours):D2}'{Math.Abs(offset.Minutes):D2}'");
    }

    private void Write(byte[] bytes, int start, string text)
    {
        for (var i = 0; i < text.Length; i++) bytes[start + i] = (byte)text[i];
    }

    private int Find(byte[] bytes, string needle, int from) =>
        bytes.AsSpan(from).IndexOf(Encoding.ASCII.GetBytes(needle)) is var found and >= 0 ? from + found : -1;
}

internal sealed class DerEnvelope
{
    public int? Length(byte[] bytes)
    {
        if (bytes.Length < 2 || bytes[0] != 0x30) return null;

        if (bytes[1] < 0x80) return 2 + bytes[1];

        var count = bytes[1] & 0x7F;
        if (count is 0 or > 4 || bytes.Length < 2 + count) return null;

        long length = 0;
        for (var i = 0; i < count; i++) length = (length << 8) | bytes[2 + i];

        return length > int.MaxValue - 6 ? null : (int)(2 + count + length);
    }
}

internal sealed record VerifiedSignature(SignerIdentity Signer, SignatureStatus Status);

internal sealed class DetachedSignature(CertificateJudge judge)
{
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    public byte[] Sign(byte[] content, X509Certificate2 certificate)
    {
        var cms = new SignedCms(new ContentInfo(content), detached: true);
        cms.ComputeSignature(new CmsSigner(certificate) { IncludeOption = X509IncludeOption.WholeChain });
        return cms.Encode();
    }

    public VerifiedSignature? Verify(byte[] content, byte[] encoded)
    {
        var cms = new SignedCms(new ContentInfo(content), detached: true);

        try
        {
            cms.Decode(encoded);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }

        if (cms.SignerInfos.Count == 0 || cms.SignerInfos[0].Certificate is not { } certificate) return null;
        return new VerifiedSignature(SignerIdentity.From(certificate), Status(cms, certificate));
    }

    private SignatureStatus Status(SignedCms cms, X509Certificate2 certificate)
    {
        try
        {
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return SignatureStatus.Broken;
        }

        var signer = cms.SignerInfos[0];
        var token = signer.UnsignedAttributes.OfType<System.Security.Cryptography.CryptographicAttributeObject>()
            .Where(attribute => attribute.Oid.Value == TimestampTokenOid)
            .SelectMany(attribute => attribute.Values.Cast<System.Security.Cryptography.AsnEncodedData>())
            .Select(data => data.RawData)
            .FirstOrDefault();

        return judge.Judge(certificate, token is null ? null : new TimestampEvidence(token, signer.GetSignature()));
    }
}
