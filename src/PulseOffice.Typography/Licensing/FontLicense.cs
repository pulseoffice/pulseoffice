namespace PulseOffice.Typography.Licensing;

public enum FontLicenseKind
{
    Unknown,
    Ofl,
    Apache,
    Mit,
    Ubuntu,
    Cc0,
    PublicDomain,
    Bitstream,
    Gpl,
    OpenSource,
    Proprietary,
}

public enum LicenseFreedom
{
    Free,
    Restricted,
    Unknown,
}

public sealed record FontLicense(FontLicenseKind Kind, string Name, string? SpdxId = null, string? Url = null, string? Text = null)
{
    public static readonly FontLicense Unknown = new(FontLicenseKind.Unknown, "Unknown");

    public LicenseFreedom Freedom => Kind switch
    {
        FontLicenseKind.Unknown => LicenseFreedom.Unknown,
        FontLicenseKind.Proprietary => LicenseFreedom.Restricted,
        _ => LicenseFreedom.Free,
    };

    public bool IsFree => Freedom == LicenseFreedom.Free;
}
