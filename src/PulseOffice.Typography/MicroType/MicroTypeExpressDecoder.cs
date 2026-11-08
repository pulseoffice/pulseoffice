namespace PulseOffice.Typography.MicroType;

public interface IMicroTypeExpressDecoder
{
    byte[] Decompress(byte[] mtx);
}

public sealed class MicroTypeExpressDecoder : IMicroTypeExpressDecoder
{
    private const int LocaFormatOffset = 50;

    private readonly MtxContainerReader _container;
    private readonly IReadOnlyList<CtfTableTranslator> _translators;
    private readonly ISfntAssembler _assembler;
    private readonly MtxLimits _limits;

    public MicroTypeExpressDecoder() : this(MtxLimits.Default)
    {
    }

    public MicroTypeExpressDecoder(MtxLimits limits) : this(new LzcompDecoder(limits), limits)
    {
    }

    public MicroTypeExpressDecoder(ILzcompDecoder lzcomp, MtxLimits limits)
    {
        ArgumentNullException.ThrowIfNull(lzcomp);
        ArgumentNullException.ThrowIfNull(limits);
        var program = new GlyphProgramDecoder();
        _limits = limits;
        _container = new MtxContainerReader(lzcomp, limits);
        _assembler = new SfntAssembler(limits);
        _translators =
        [
            new GlyfTableTranslator([new SimpleGlyphDecoder(new TripletTable(), program), new CompositeGlyphDecoder(program)]),
            new LocaTableTranslator(),
            new CvtTableTranslator(),
            new HdmxTableTranslator(),
            new VdmxTableTranslator(),
            new CopiedTableTranslator()
        ];
    }

    public byte[] Decompress(byte[] mtx)
    {
        ArgumentNullException.ThrowIfNull(mtx);
        try
        {
            var font = new CtfFont(_container.Read(mtx), _limits);
            var tables = new List<SfntTable>();
            foreach (var entry in font.Entries)
            {
                var translator = _translators.First(candidate => candidate.Handles(entry.Tag));
                tables.AddRange(translator.Translate(font, entry));
            }

            if (tables.Select(table => table.Tag).Distinct(StringComparer.Ordinal).Count() != tables.Count)
                throw new InvalidDataException("Compact font produces the same table twice.");

            MarkLocaFormat(font, tables);
            return _assembler.Assemble(font.ScalarType, tables);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or OverflowException or InvalidCastException)
        {
            throw new InvalidDataException("MicroType Express font is malformed.", ex);
        }
    }

    private void MarkLocaFormat(CtfFont font, List<SfntTable> tables)
    {
        if (!tables.Any(table => table.Tag == "glyf")) return;
        var head = tables.FirstOrDefault(table => table.Tag == "head")?.Data;
        if (head is null || head.Length < LocaFormatOffset + 2) throw new InvalidDataException("Compact font has no usable 'head' table.");

        head[LocaFormatOffset] = 0;
        head[LocaFormatOffset + 1] = font.LongLoca ? (byte)1 : (byte)0;
    }
}
