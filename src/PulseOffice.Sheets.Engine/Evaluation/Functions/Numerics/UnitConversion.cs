namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public enum UnitFamily { Mass, Length, Time, Pressure, Force, Energy, Power, Magnetism, Temperature, Volume, Area, Speed, Information }

public interface IUnitConversion
{
    double? Convert(double value, string from, string to);
}

public abstract class MeasurementUnit(string name, double perBase, UnitFamily family, bool prefixes)
{
    public const int NoMatch = short.MinValue;

    private const int BinaryStep = 10;

    public string Name => name;

    public UnitFamily Family => family;

    protected double PerBase => perBase;

    public int Level(string reference)
    {
        var text = reference;
        var caret = reference.LastIndexOf('^');
        if (caret > 0 && caret == reference.Length - 2) text = reference.Remove(caret, 1);
        if (text == name) return 0;
        if (!prefixes || !text.EndsWith(name, StringComparison.Ordinal)) return NoMatch;

        var prefix = text[..^name.Length];
        if (prefix.Length == 1 || prefix == "da")
        {
            int? level = prefix[0] switch
            {
                'y' => -24, 'z' => -21, 'a' => -18, 'f' => -15, 'p' => -12, 'n' => -9, 'u' => -6, 'm' => -3, 'c' => -2,
                'd' => prefix.Length == 1 ? -1 : 1,
                'e' => 1, 'h' => 2, 'k' => 3, 'M' => 6, 'G' => 9, 'T' => 12, 'P' => 15, 'E' => 18, 'Z' => 21, 'Y' => 24,
                _ => null,
            };
            if (level is not { } found) return NoMatch;
            if (text.EndsWith('2')) return found * 2;
            if (text.EndsWith('3')) return found * 3;
            return found;
        }

        if (prefix.Length == 2 && prefix[1] == 'i' && family == UnitFamily.Information)
            return prefix[0] switch
            {
                'k' => BinaryStep, 'M' => 2 * BinaryStep, 'G' => 3 * BinaryStep, 'T' => 4 * BinaryStep,
                'P' => 5 * BinaryStep, 'E' => 6 * BinaryStep, 'Z' => 7 * BinaryStep, 'Y' => 8 * BinaryStep,
                _ => NoMatch,
            };
        return NoMatch;
    }

    public abstract double Convert(double value, MeasurementUnit target, int fromLevel, int toLevel);

    protected double Scale(double value, int exponent) => exponent == 0 ? value : value * System.Math.Pow(10, exponent);

    protected bool IsBinary(int level) => level > 0 && level % BinaryStep == 0;
}

public sealed class ProportionalUnit(string name, double perBase, UnitFamily family, bool prefixes = false)
    : MeasurementUnit(name, perBase, family, prefixes)
{
    public override double Convert(double value, MeasurementUnit target, int fromLevel, int toLevel)
    {
        var result = value * ((ProportionalUnit)target).PerBase / PerBase;
        if (Family == UnitFamily.Information && (IsBinary(fromLevel) || IsBinary(toLevel)))
        {
            if (IsBinary(fromLevel) && IsBinary(toLevel)) return result * System.Math.Pow(2, fromLevel - toLevel);
            if (IsBinary(fromLevel)) return result * System.Math.Pow(2, fromLevel) / System.Math.Pow(10, toLevel);
            return result * System.Math.Pow(10, fromLevel) / System.Math.Pow(2, toLevel);
        }
        return Scale(result, fromLevel - toLevel);
    }
}

public sealed class LinearUnit(string name, double perBase, double offset, UnitFamily family, bool prefixes = false)
    : MeasurementUnit(name, perBase, family, prefixes)
{
    public override double Convert(double value, MeasurementUnit target, int fromLevel, int toLevel) =>
        ((LinearUnit)target).FromBase(ToBase(value, fromLevel), toLevel);

    private double ToBase(double value, int level) => Scale(value, level) / PerBase - offset;

    private double FromBase(double value, int level) => Scale((value + offset) * PerBase, -level);
}

public sealed class UnitConversion : IUnitConversion
{
    private readonly IReadOnlyList<MeasurementUnit> _units =
    [
        new ProportionalUnit("g", 1.0, UnitFamily.Mass, true),
        new ProportionalUnit("sg", 6.8522050005347800E-05, UnitFamily.Mass),
        new ProportionalUnit("lbm", 2.2046229146913400E-03, UnitFamily.Mass),
        new ProportionalUnit("u", 6.0221370000000000E23, UnitFamily.Mass, true),
        new ProportionalUnit("ozm", 3.5273971800362700E-02, UnitFamily.Mass),
        new ProportionalUnit("stone", 1.574730e-04, UnitFamily.Mass),
        new ProportionalUnit("ton", 1.102311e-06, UnitFamily.Mass),
        new ProportionalUnit("grain", 1.543236E01, UnitFamily.Mass),
        new ProportionalUnit("pweight", 7.054792E-01, UnitFamily.Mass),
        new ProportionalUnit("hweight", 1.968413E-05, UnitFamily.Mass),
        new ProportionalUnit("shweight", 2.204623E-05, UnitFamily.Mass),
        new ProportionalUnit("brton", 9.842065E-07, UnitFamily.Mass),
        new ProportionalUnit("cwt", 2.2046226218487758E-05, UnitFamily.Mass),
        new ProportionalUnit("shweight", 2.2046226218487758E-05, UnitFamily.Mass),
        new ProportionalUnit("uk_cwt", 1.9684130552221213E-05, UnitFamily.Mass),
        new ProportionalUnit("lcwt", 1.9684130552221213E-05, UnitFamily.Mass),
        new ProportionalUnit("hweight", 1.9684130552221213E-05, UnitFamily.Mass),
        new ProportionalUnit("uk_ton", 9.8420652761106063E-07, UnitFamily.Mass),
        new ProportionalUnit("LTON", 9.8420652761106063E-07, UnitFamily.Mass),

        new ProportionalUnit("m", 1.0, UnitFamily.Length, true),
        new ProportionalUnit("mi", 6.2137119223733397E-04, UnitFamily.Length),
        new ProportionalUnit("Nmi", 5.3995680345572354E-04, UnitFamily.Length),
        new ProportionalUnit("in", 3.9370078740157480E01, UnitFamily.Length),
        new ProportionalUnit("ft", 3.2808398950131234E00, UnitFamily.Length),
        new ProportionalUnit("yd", 1.0936132983377078E00, UnitFamily.Length),
        new ProportionalUnit("ang", 1.0E10, UnitFamily.Length, true),
        new ProportionalUnit("Pica", 2.8346456692913386E03, UnitFamily.Length),
        new ProportionalUnit("picapt", 2.8346456692913386E03, UnitFamily.Length),
        new ProportionalUnit("pica", 2.36220472441E02, UnitFamily.Length),
        new ProportionalUnit("ell", 8.748906E-01, UnitFamily.Length),
        new ProportionalUnit("parsec", 3.240779E-17, UnitFamily.Length, true),
        new ProportionalUnit("pc", 3.240779E-17, UnitFamily.Length, true),
        new ProportionalUnit("lightyear", 1.0570234557732930E-16, UnitFamily.Length, true),
        new ProportionalUnit("ly", 1.0570234557732930E-16, UnitFamily.Length, true),
        new ProportionalUnit("survey_mi", 6.2136994949494949E-04, UnitFamily.Length),

        new ProportionalUnit("yr", 3.1688087814028950E-08, UnitFamily.Time),
        new ProportionalUnit("day", 1.1574074074074074E-05, UnitFamily.Time),
        new ProportionalUnit("d", 1.1574074074074074E-05, UnitFamily.Time),
        new ProportionalUnit("hr", 2.7777777777777778E-04, UnitFamily.Time),
        new ProportionalUnit("mn", 1.6666666666666667E-02, UnitFamily.Time),
        new ProportionalUnit("min", 1.6666666666666667E-02, UnitFamily.Time),
        new ProportionalUnit("sec", 1.0, UnitFamily.Time, true),
        new ProportionalUnit("s", 1.0, UnitFamily.Time, true),

        new ProportionalUnit("Pa", 1.0, UnitFamily.Pressure, true),
        new ProportionalUnit("atm", 9.8692329999819300E-06, UnitFamily.Pressure, true),
        new ProportionalUnit("at", 9.8692329999819300E-06, UnitFamily.Pressure, true),
        new ProportionalUnit("mmHg", 7.5006170799862700E-03, UnitFamily.Pressure, true),
        new ProportionalUnit("Torr", 7.5006380000000000E-03, UnitFamily.Pressure),
        new ProportionalUnit("psi", 1.4503770000000000E-04, UnitFamily.Pressure),

        new ProportionalUnit("N", 1.0, UnitFamily.Force, true),
        new ProportionalUnit("dyn", 1.0E05, UnitFamily.Force, true),
        new ProportionalUnit("dy", 1.0E05, UnitFamily.Force, true),
        new ProportionalUnit("lbf", 2.24808923655339E-01, UnitFamily.Force),
        new ProportionalUnit("pond", 1.019716E02, UnitFamily.Force, true),

        new ProportionalUnit("J", 1.0, UnitFamily.Energy, true),
        new ProportionalUnit("e", 1.0E07, UnitFamily.Energy, true),
        new ProportionalUnit("c", 2.3900624947346700E-01, UnitFamily.Energy, true),
        new ProportionalUnit("cal", 2.3884619064201700E-01, UnitFamily.Energy, true),
        new ProportionalUnit("eV", 6.2414570000000000E18, UnitFamily.Energy, true),
        new ProportionalUnit("ev", 6.2414570000000000E18, UnitFamily.Energy, true),
        new ProportionalUnit("HPh", 3.7250611111111111E-07, UnitFamily.Energy),
        new ProportionalUnit("hh", 3.7250611111111111E-07, UnitFamily.Energy),
        new ProportionalUnit("Wh", 2.7777777777777778E-04, UnitFamily.Energy, true),
        new ProportionalUnit("wh", 2.7777777777777778E-04, UnitFamily.Energy, true),
        new ProportionalUnit("flb", 2.37304222192651E01, UnitFamily.Energy),
        new ProportionalUnit("BTU", 9.4781506734901500E-04, UnitFamily.Energy),
        new ProportionalUnit("btu", 9.4781506734901500E-04, UnitFamily.Energy),

        new ProportionalUnit("W", 1.0, UnitFamily.Power, true),
        new ProportionalUnit("w", 1.0, UnitFamily.Power, true),
        new ProportionalUnit("HP", 1.341022E-03, UnitFamily.Power),
        new ProportionalUnit("h", 1.341022E-03, UnitFamily.Power),
        new ProportionalUnit("PS", 1.359622E-03, UnitFamily.Power),

        new ProportionalUnit("T", 1.0, UnitFamily.Magnetism, true),
        new ProportionalUnit("ga", 1.0E04, UnitFamily.Magnetism, true),

        new LinearUnit("C", 1.0, -2.7315000000000000E02, UnitFamily.Temperature),
        new LinearUnit("cel", 1.0, -2.7315000000000000E02, UnitFamily.Temperature),
        new LinearUnit("F", 1.8, -2.5537222222222222E02, UnitFamily.Temperature),
        new LinearUnit("fah", 1.8, -2.5537222222222222E02, UnitFamily.Temperature),
        new LinearUnit("K", 1.0, 0.0, UnitFamily.Temperature, true),
        new LinearUnit("kel", 1.0, 0.0, UnitFamily.Temperature, true),
        new LinearUnit("Reau", 8.0E-01, -2.7315000000000000E02, UnitFamily.Temperature),
        new LinearUnit("Rank", 1.8, 0.0, UnitFamily.Temperature),

        new ProportionalUnit("tsp", 2.0288413621105798E02, UnitFamily.Volume),
        new ProportionalUnit("tbs", 6.7628045403685994E01, UnitFamily.Volume),
        new ProportionalUnit("oz", 3.3814022701842997E01, UnitFamily.Volume),
        new ProportionalUnit("cup", 4.2267528377303746E00, UnitFamily.Volume),
        new ProportionalUnit("pt", 2.1133764188651873E00, UnitFamily.Volume),
        new ProportionalUnit("us_pt", 2.1133764188651873E00, UnitFamily.Volume),
        new ProportionalUnit("uk_pt", 1.7597539863927023E00, UnitFamily.Volume),
        new ProportionalUnit("qt", 1.0566882094325937E00, UnitFamily.Volume),
        new ProportionalUnit("gal", 2.6417205235814842E-01, UnitFamily.Volume),
        new ProportionalUnit("l", 1.0, UnitFamily.Volume, true),
        new ProportionalUnit("L", 1.0, UnitFamily.Volume, true),
        new ProportionalUnit("lt", 1.0, UnitFamily.Volume, true),
        new ProportionalUnit("m3", 1.0E-03, UnitFamily.Volume, true),
        new ProportionalUnit("mi3", 2.3991275857892772E-13, UnitFamily.Volume),
        new ProportionalUnit("Nmi3", 1.5742621468581148E-13, UnitFamily.Volume),
        new ProportionalUnit("in3", 6.1023744094732284E01, UnitFamily.Volume),
        new ProportionalUnit("ft3", 3.5314666721488590E-02, UnitFamily.Volume),
        new ProportionalUnit("yd3", 1.3079506193143922E-03, UnitFamily.Volume),
        new ProportionalUnit("ang3", 1.0E27, UnitFamily.Volume, true),
        new ProportionalUnit("Pica3", 2.2776990435870636E07, UnitFamily.Volume),
        new ProportionalUnit("picapt3", 2.2776990435870636E07, UnitFamily.Volume),
        new ProportionalUnit("pica3", 1.31811287245E04, UnitFamily.Volume),
        new ProportionalUnit("barrel", 6.2898107704321051E-03, UnitFamily.Volume),
        new ProportionalUnit("bushel", 2.837759E-02, UnitFamily.Volume),
        new ProportionalUnit("regton", 3.531467E-04, UnitFamily.Volume),
        new ProportionalUnit("GRT", 3.531467E-04, UnitFamily.Volume),
        new ProportionalUnit("Schooner", 2.3529411764705882E00, UnitFamily.Volume),
        new ProportionalUnit("Middy", 3.5087719298245614E00, UnitFamily.Volume),
        new ProportionalUnit("Glass", 5.0, UnitFamily.Volume),
        new ProportionalUnit("Sixpack", 0.5, UnitFamily.Volume),
        new ProportionalUnit("Humpen", 2.0, UnitFamily.Volume),
        new ProportionalUnit("ly3", 1.1810108125623799E-51, UnitFamily.Volume),
        new ProportionalUnit("MTON", 1.4125866688595436E00, UnitFamily.Volume),
        new ProportionalUnit("tspm", 2.0E02, UnitFamily.Volume),
        new ProportionalUnit("uk_gal", 2.1996924829908779E-01, UnitFamily.Volume),
        new ProportionalUnit("uk_qt", 8.7987699319635115E-01, UnitFamily.Volume),

        new ProportionalUnit("m2", 1.0, UnitFamily.Area, true),
        new ProportionalUnit("mi2", 3.8610215854244585E-07, UnitFamily.Area),
        new ProportionalUnit("Nmi2", 2.9155334959812286E-07, UnitFamily.Area),
        new ProportionalUnit("in2", 1.5500031000062000E03, UnitFamily.Area),
        new ProportionalUnit("ft2", 1.0763910416709722E01, UnitFamily.Area),
        new ProportionalUnit("yd2", 1.1959900463010803E00, UnitFamily.Area),
        new ProportionalUnit("ang2", 1.0E20, UnitFamily.Area, true),
        new ProportionalUnit("Pica2", 8.0352160704321409E06, UnitFamily.Area),
        new ProportionalUnit("picapt2", 8.0352160704321409E06, UnitFamily.Area),
        new ProportionalUnit("pica2", 5.58001116002232E04, UnitFamily.Area),
        new ProportionalUnit("Morgen", 4.0E-04, UnitFamily.Area),
        new ProportionalUnit("ar", 1.0E-02, UnitFamily.Area, true),
        new ProportionalUnit("acre", 2.471053815E-04, UnitFamily.Area),
        new ProportionalUnit("uk_acre", 2.4710538146716534E-04, UnitFamily.Area),
        new ProportionalUnit("us_acre", 2.4710439304662790E-04, UnitFamily.Area),
        new ProportionalUnit("ly2", 1.1172985860549147E-32, UnitFamily.Area),
        new ProportionalUnit("ha", 1.0E-04, UnitFamily.Area),

        new ProportionalUnit("m/s", 1.0, UnitFamily.Speed, true),
        new ProportionalUnit("m/sec", 1.0, UnitFamily.Speed, true),
        new ProportionalUnit("m/h", 3.6E03, UnitFamily.Speed, true),
        new ProportionalUnit("m/hr", 3.6E03, UnitFamily.Speed, true),
        new ProportionalUnit("mph", 2.2369362920544023E00, UnitFamily.Speed),
        new ProportionalUnit("kn", 1.9438444924406048E00, UnitFamily.Speed),
        new ProportionalUnit("admkn", 1.9438446603753486E00, UnitFamily.Speed),
        new ProportionalUnit("ludicrous speed", 2.0494886343432328E-14, UnitFamily.Speed),
        new ProportionalUnit("ridiculous speed", 4.0156958471424288E-06, UnitFamily.Speed),

        new ProportionalUnit("bit", 1.0, UnitFamily.Information, true),
        new ProportionalUnit("byte", 1.25E-01, UnitFamily.Information, true),
    ];

    public double? Convert(double value, string from, string to)
    {
        MeasurementUnit? source = null, target = null;
        int fromLevel = 0, toLevel = 0;
        bool searchFrom = true, searchTo = true;

        foreach (var unit in _units)
        {
            if (searchFrom && unit.Level(from) is var level and not MeasurementUnit.NoMatch)
            {
                (source, fromLevel) = (unit, level);
                searchFrom = level != 0;
            }
            if (searchTo && unit.Level(to) is var toFound and not MeasurementUnit.NoMatch)
            {
                (target, toLevel) = (unit, toFound);
                searchTo = toFound != 0;
            }
            if (!searchFrom && !searchTo) break;
        }

        if (source is null || target is null || source.Family != target.Family) return null;
        return source.Convert(value, target, fromLevel, toLevel);
    }
}
