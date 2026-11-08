namespace PulseOffice.Formats.Packaging.Encryption;

public sealed class OdfDerivationBudget(long pbkdf2Iterations, long argon2KibibytePasses)
{
    public const long DefaultPbkdf2Iterations = 1L << 28;

    public const long DefaultArgon2KibibytePasses = 1L << 28;

    public OdfDerivationBudget() : this(DefaultPbkdf2Iterations, DefaultArgon2KibibytePasses)
    {
    }

    public bool Allows(IEnumerable<OdfEncryptionData> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        long pbkdf2 = 0;
        long argon2 = 0;
        foreach (var entry in entries)
        {
            if (entry.Argon2 is { } argon) argon2 += (long)Math.Max(argon.Iterations, 0) * Math.Max(argon.MemoryKiB, 0);
            else pbkdf2 += Math.Max(entry.IterationCount, 0);
            if (pbkdf2 > pbkdf2Iterations || argon2 > argon2KibibytePasses) return false;
        }

        return true;
    }
}
