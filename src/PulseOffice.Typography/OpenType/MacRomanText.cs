namespace PulseOffice.Typography.OpenType;

internal sealed class MacRomanText
{
    private const string Upper =
        "ÄÅÇÉÑÖÜáàâäãåçéèêëíìîïñóòôöõúùûü†°¢£§•¶ß®©™´¨≠ÆØ∞±≤≥¥µ∂∑∏π∫ªºΩæø" +
        "¿¡¬√ƒ≈∆«»… ÀÃÕŒœ–—“”‘’÷◊ÿŸ⁄€‹›ﬁﬂ‡·‚„‰ÂÊÁËÈÍÎÏÌÓÔÒÚÛÙıˆ˜¯˘˙˚¸˝˛ˇ";

    public string Decode(ReadOnlySpan<byte> bytes) =>
        string.Create(bytes.Length, bytes.ToArray(), (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
                span[index] = source[index] < 0x80 ? (char)source[index] : Upper[source[index] - 0x80];
        });
}
