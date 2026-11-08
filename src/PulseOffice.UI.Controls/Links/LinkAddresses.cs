namespace PulseOffice.UI.Controls.Links;

public interface ILinkAddresses
{
    LinkKind Classify(string address, IReadOnlyList<LinkPlace> places);

    LinkPlace? FindPlace(string address, IReadOnlyList<LinkPlace> places);

    string Web(string typed);

    string Mail(MailAddress mail);

    MailAddress ReadMail(string address);

    string File(string path);

    string ReadFile(string address);

    bool IsComplete(LinkKind kind, string address);
}

public sealed class LinkAddresses : ILinkAddresses
{
    private const string MailScheme = "mailto:";
    private const string SubjectKey = "subject=";

    private readonly string[] _webSchemes = ["http://", "https://", "ftp://", "ftps://"];

    public LinkKind Classify(string address, IReadOnlyList<LinkPlace> places)
    {
        ArgumentNullException.ThrowIfNull(address);
        var trimmed = address.Trim();
        if (trimmed.Length == 0) return LinkKind.Web;
        if (trimmed.StartsWith(MailScheme, StringComparison.OrdinalIgnoreCase)) return LinkKind.Mail;
        if (trimmed.StartsWith('#') || FindPlace(trimmed, places) is not null) return LinkKind.Place;
        if (trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return LinkKind.File;
        if (_webSchemes.Any(scheme => trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))) return LinkKind.Web;
        if (Path.IsPathRooted(trimmed) || trimmed.StartsWith("./", StringComparison.Ordinal) || trimmed.StartsWith("../", StringComparison.Ordinal)) return LinkKind.File;
        return LinkKind.Web;
    }

    public LinkPlace? FindPlace(string address, IReadOnlyList<LinkPlace> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        foreach (var place in places)
        {
            if (place.Selectable && string.Equals(place.Address, address, StringComparison.Ordinal)) return place;
            if (FindPlace(address, place.Children) is { } nested) return nested;
        }
        return null;
    }

    public string Web(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var trimmed = typed.Trim();
        if (trimmed.Length == 0 || trimmed.Contains("://", StringComparison.Ordinal) || trimmed.StartsWith(MailScheme, StringComparison.OrdinalIgnoreCase)) return trimmed;
        return trimmed.Contains('.', StringComparison.Ordinal) && !trimmed.Contains(' ', StringComparison.Ordinal) ? "https://" + trimmed : trimmed;
    }

    public string Mail(MailAddress mail)
    {
        ArgumentNullException.ThrowIfNull(mail);
        var to = mail.To.Trim();
        if (to.StartsWith(MailScheme, StringComparison.OrdinalIgnoreCase)) to = to[MailScheme.Length..];
        if (to.Length == 0) return string.Empty;
        var subject = mail.Subject.Trim();
        return MailScheme + to + (subject.Length > 0 ? "?" + SubjectKey + Uri.EscapeDataString(subject) : string.Empty);
    }

    public MailAddress ReadMail(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var trimmed = address.Trim();
        if (!trimmed.StartsWith(MailScheme, StringComparison.OrdinalIgnoreCase)) return new MailAddress(string.Empty, string.Empty);
        var body = trimmed[MailScheme.Length..];
        var query = body.IndexOf('?', StringComparison.Ordinal);
        if (query < 0) return new MailAddress(body, string.Empty);
        var subject = body[(query + 1)..].Split('&')
            .Where(part => part.StartsWith(SubjectKey, StringComparison.OrdinalIgnoreCase))
            .Select(part => Uri.UnescapeDataString(part[SubjectKey.Length..]))
            .FirstOrDefault() ?? string.Empty;
        return new MailAddress(body[..query], subject);
    }

    public string File(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = path.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return trimmed;
        return Path.IsPathRooted(trimmed) && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.AbsoluteUri
            : trimmed.Replace('\\', '/');
    }

    public string ReadFile(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var trimmed = address.Trim();
        return trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.LocalPath : trimmed;
    }

    public bool IsComplete(LinkKind kind, string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var trimmed = address.Trim();
        return kind == LinkKind.Mail
            ? trimmed.Length > MailScheme.Length && trimmed.Contains('@', StringComparison.Ordinal)
            : trimmed.Length > 0;
    }
}
