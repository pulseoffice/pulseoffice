using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Sheets;

public sealed record WorkbookProtection
{
    public bool Structure { get; init; }

    public bool Windows { get; init; }

    public string? PasswordHash { get; init; }

    public bool IsEnabled => Structure || Windows;

    public static readonly WorkbookProtection None = new();

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }

    public bool Accepts(string? password) =>
        PasswordHash is null || (password is not null && PasswordKey.Parse(PasswordHash).Matches(password));
}
