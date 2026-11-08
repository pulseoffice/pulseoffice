using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PulseOffice.Formats.Pdf;

public enum PdfPrintPermission
{
    None,
    LowResolution,
    HighResolution,
}

public sealed record PdfSecurityOptions
{
    public string OpenPassword { get; init; } = string.Empty;

    public string PermissionsPassword { get; init; } = string.Empty;

    public PdfPrintPermission Printing { get; init; } = PdfPrintPermission.HighResolution;

    public bool AllowCopy { get; init; } = true;

    public bool AllowChanges { get; init; } = true;

    public bool AllowComments { get; init; } = true;

    public bool Restricts =>
        Printing != PdfPrintPermission.HighResolution || !AllowCopy || !AllowChanges || !AllowComments;

    public bool Encrypts => OpenPassword.Length > 0 || PermissionsPassword.Length > 0 || Restricts;

    public int PermissionFlags
    {
        get
        {
            var flags = unchecked((int)0xFFFFF0C0);
            if (Printing != PdfPrintPermission.None) flags |= 1 << 2;
            if (Printing == PdfPrintPermission.HighResolution) flags |= 1 << 11;
            if (AllowChanges) flags |= (1 << 3) | (1 << 10);
            if (AllowCopy) flags |= 1 << 4;
            flags |= 1 << 9;
            if (AllowComments) flags |= (1 << 5) | (1 << 8);
            return flags;
        }
    }
}

public interface IPdfObjectSealer
{
    byte[] Seal(byte[] payload);

    string SealStrings(string body);

    string EncryptDictionary { get; }
}

public interface IPdfEncryptionFactory
{
    IPdfObjectSealer? Create(PdfSecurityOptions? options);
}

public interface IPdfPasswordHash
{
    byte[] Hash(byte[] password, byte[] salt, byte[] userKey);

    byte[] Prepare(string password);
}

public sealed class Revision6PasswordHash : IPdfPasswordHash
{
    private const int MaxPasswordBytes = 127;

    public byte[] Prepare(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var bytes = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormKC));
        return bytes.Length > MaxPasswordBytes ? bytes[..MaxPasswordBytes] : bytes;
    }

    public byte[] Hash(byte[] password, byte[] salt, byte[] userKey)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(userKey);

        var key = SHA256.HashData([.. password, .. salt, .. userKey]);
        var round = 0;
        byte[] encrypted = [];
        while (round < 64 || encrypted[^1] > round - 32)
        {
            byte[] block = [.. password, .. key, .. userKey];
            var repeated = new byte[block.Length * 64];
            for (var copy = 0; copy < 64; copy++) block.CopyTo(repeated, copy * block.Length);

            using var aes = Aes.Create();
            aes.Key = key[..16];
            encrypted = aes.EncryptCbc(repeated, key[16..32], PaddingMode.None);

            var remainder = 0;
            for (var index = 0; index < 16; index++) remainder += encrypted[index];
            key = (remainder % 3) switch
            {
                0 => SHA256.HashData(encrypted),
                1 => SHA384.HashData(encrypted),
                _ => SHA512.HashData(encrypted),
            };
            round++;
        }

        return key[..32];
    }
}

public sealed class Aes256EncryptionFactory(IPdfPasswordHash hash) : IPdfEncryptionFactory
{
    public Aes256EncryptionFactory() : this(new Revision6PasswordHash())
    {
    }

    public IPdfObjectSealer? Create(PdfSecurityOptions? options)
    {
        if (options is null || !options.Encrypts) return null;

        var fileKey = RandomNumberGenerator.GetBytes(32);
        var user = hash.Prepare(options.OpenPassword);
        var owner = hash.Prepare(options.PermissionsPassword.Length > 0
            ? options.PermissionsPassword
            : Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

        var userValidation = RandomNumberGenerator.GetBytes(8);
        var userKeySalt = RandomNumberGenerator.GetBytes(8);
        byte[] u = [.. hash.Hash(user, userValidation, []), .. userValidation, .. userKeySalt];
        var ue = Wrap(hash.Hash(user, userKeySalt, []), fileKey);

        var ownerValidation = RandomNumberGenerator.GetBytes(8);
        var ownerKeySalt = RandomNumberGenerator.GetBytes(8);
        byte[] o = [.. hash.Hash(owner, ownerValidation, u), .. ownerValidation, .. ownerKeySalt];
        var oe = Wrap(hash.Hash(owner, ownerKeySalt, u), fileKey);

        var flags = options.PermissionFlags;
        var block = new byte[16];
        BitConverter.TryWriteBytes(block.AsSpan(0, 4), flags);
        if (!BitConverter.IsLittleEndian) block.AsSpan(0, 4).Reverse();
        block[4] = block[5] = block[6] = block[7] = 0xFF;
        block[8] = (byte)'T';
        block[9] = (byte)'a';
        block[10] = (byte)'d';
        block[11] = (byte)'b';
        RandomNumberGenerator.GetBytes(4).CopyTo(block, 12);
        using var aes = Aes.Create();
        aes.Key = fileKey;
        var perms = aes.EncryptEcb(block, PaddingMode.None);

        var dictionary = string.Create(CultureInfo.InvariantCulture,
            $"<< /Filter /Standard /V 5 /R 6 /Length 256 /CF << /StdCF << /Type /CryptFilter /CFM /AESV3 /AuthEvent /DocOpen /Length 32 >> >> /StmF /StdCF /StrF /StdCF " +
            $"/O <{Convert.ToHexString(o)}> /U <{Convert.ToHexString(u)}> /OE <{Convert.ToHexString(oe)}> /UE <{Convert.ToHexString(ue)}> " +
            $"/P {flags} /Perms <{Convert.ToHexString(perms)}> /EncryptMetadata true >>");

        return new Aes256ObjectSealer(fileKey, dictionary, new PdfStringTokens());
    }

    private byte[] Wrap(byte[] key, byte[] fileKey)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.EncryptCbc(fileKey, new byte[16], PaddingMode.None);
    }
}

public sealed class Aes256ObjectSealer(byte[] fileKey, string encryptDictionary, IPdfStringTokens tokens) : IPdfObjectSealer
{
    public string EncryptDictionary => encryptDictionary;

    public byte[] Seal(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var aes = Aes.Create();
        aes.Key = fileKey;
        var iv = RandomNumberGenerator.GetBytes(16);
        return [.. iv, .. aes.EncryptCbc(payload, iv, PaddingMode.PKCS7)];
    }

    public string SealStrings(string body) => tokens.Replace(body, bytes => "<" + Convert.ToHexString(Seal(bytes)) + ">");
}

public interface IPdfStringTokens
{
    string Replace(string body, Func<byte[], string> replacement);
}

public sealed class PdfStringTokens : IPdfStringTokens
{
    public string Replace(string body, Func<byte[], string> replacement)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(replacement);

        var result = new StringBuilder(body.Length);
        var index = 0;
        while (index < body.Length)
        {
            var character = body[index];
            if (character == '(')
            {
                result.Append(replacement(Literal(body, ref index)));
            }
            else if (character == '<' && index + 1 < body.Length && body[index + 1] == '<')
            {
                result.Append("<<");
                index += 2;
            }
            else if (character == '<')
            {
                result.Append(replacement(Hex(body, ref index)));
            }
            else
            {
                result.Append(character);
                index++;
            }
        }

        return result.ToString();
    }

    private byte[] Literal(string body, ref int index)
    {
        var bytes = new List<byte>();
        var depth = 0;
        index++;
        while (index < body.Length)
        {
            var character = body[index++];
            if (character == '\\' && index < body.Length)
            {
                var escaped = body[index++];
                switch (escaped)
                {
                    case 'n': bytes.Add((byte)'\n'); break;
                    case 'r': bytes.Add((byte)'\r'); break;
                    case 't': bytes.Add((byte)'\t'); break;
                    case 'b': bytes.Add((byte)'\b'); break;
                    case 'f': bytes.Add((byte)'\f'); break;
                    case '\r':
                        if (index < body.Length && body[index] == '\n') index++;
                        break;
                    case '\n': break;
                    case >= '0' and <= '7':
                        var value = escaped - '0';
                        for (var digit = 0; digit < 2 && index < body.Length && body[index] is >= '0' and <= '7'; digit++)
                            value = value * 8 + (body[index++] - '0');
                        bytes.Add((byte)value);
                        break;
                    default: bytes.Add((byte)escaped); break;
                }
            }
            else if (character == '(')
            {
                depth++;
                bytes.Add((byte)character);
            }
            else if (character == ')')
            {
                if (depth == 0) break;
                depth--;
                bytes.Add((byte)character);
            }
            else
            {
                bytes.Add((byte)character);
            }
        }

        return [.. bytes];
    }

    private byte[] Hex(string body, ref int index)
    {
        var end = body.IndexOf('>', index);
        if (end < 0) end = body.Length;
        var digits = new string(body.AsSpan(index + 1, end - index - 1).ToArray().Where(Uri.IsHexDigit).ToArray());
        if (digits.Length % 2 == 1) digits += "0";
        index = Math.Min(body.Length, end + 1);
        return Convert.FromHexString(digits);
    }
}
