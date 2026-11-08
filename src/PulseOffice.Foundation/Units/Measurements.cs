namespace PulseOffice.Foundation.Units;

public readonly record struct Twip(int Value) : IComparable<Twip>
{
    public const int PerInch = 1440;
    public const int PerPoint = 20;
    public const double PerCentimeter = PerInch / 2.54;

    public static readonly Twip Zero = new(0);

    public static Twip FromPoints(double points) => new((int)Math.Round(points * PerPoint));
    public static Twip FromInches(double inches) => new((int)Math.Round(inches * PerInch));
    public static Twip FromCentimeters(double cm) => new((int)Math.Round(cm * PerCentimeter));
    public static Twip FromMillimeters(double mm) => FromCentimeters(mm / 10.0);

    public double ToPoints() => Value / (double)PerPoint;
    public double ToInches() => Value / (double)PerInch;
    public double ToCentimeters() => Value / PerCentimeter;

    public static Twip operator +(Twip a, Twip b) => new(a.Value + b.Value);
    public static Twip operator -(Twip a, Twip b) => new(a.Value - b.Value);
    public static Twip operator *(Twip a, int k) => new(a.Value * k);
    public static Twip operator -(Twip a) => new(-a.Value);

    public int CompareTo(Twip other) => Value.CompareTo(other.Value);
    public override string ToString() => $"{Value}twip";
}

public readonly record struct Emu(long Value) : IComparable<Emu>
{
    public const long PerInch = 914_400;
    public const long PerPoint = 12_700;
    public const long PerCentimeter = 360_000;
    public const long PerTwip = PerInch / Twip.PerInch;

    public static readonly Emu Zero = new(0);

    public static Emu FromPoints(double points) => new((long)Math.Round(points * PerPoint));
    public static Emu FromCentimeters(double cm) => new((long)Math.Round(cm * PerCentimeter));
    public static Emu FromTwips(Twip twips) => new(twips.Value * PerTwip);

    public double ToPoints() => Value / (double)PerPoint;
    public double ToCentimeters() => Value / (double)PerCentimeter;

    public Twip ToTwips() => new((int)Math.Round(Value / (double)PerTwip));

    public static Emu operator +(Emu a, Emu b) => new(a.Value + b.Value);
    public static Emu operator -(Emu a, Emu b) => new(a.Value - b.Value);

    public int CompareTo(Emu other) => Value.CompareTo(other.Value);
    public override string ToString() => $"{Value}emu";
}
