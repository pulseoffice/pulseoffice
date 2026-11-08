using PulseOffice.Sheets.Engine.Evaluation.Functions;

namespace PulseOffice.Sheets.Engine.Evaluation;

public static class FunctionInventory
{
    public static readonly IReadOnlySet<string> Core = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ABS", "ACOS", "ACOSH", "ACOT", "ACOTH", "ADDRESS",
        "AGGREGATE", "AND", "ARABIC", "AREAS", "ARRAYTOTEXT", "ASC",
        "ASIN", "ASINH", "ATAN", "ATAN2", "ATANH", "AVEDEV",
        "AVERAGE", "AVERAGEA", "AVERAGEIF", "AVERAGEIFS", "B", "BAHTTEXT",
        "BASE", "BETA.DIST", "BETA.INV", "BETADIST", "BETAINV", "BINOM.DIST",
        "BINOM.INV", "BINOMDIST", "BITAND", "BITLSHIFT", "BITOR", "BITRSHIFT",
        "BITXOR", "BYCOL", "BYROW", "CEILING", "CEILING.MATH", "CEILING.PRECISE",
        "CEILING.XCL", "CELL", "CHAR", "CHIDIST", "CHIINV", "CHISQ.DIST",
        "CHISQ.DIST.RT", "CHISQ.INV", "CHISQ.INV.RT", "CHISQ.TEST", "CHISQDIST", "CHISQINV",
        "CHITEST", "CHOOSE", "CHOOSECOLS", "CHOOSEROWS", "CLEAN", "CODE",
        "COLOR", "COLUMN", "COLUMNS", "COMBIN", "COMBINA", "CONCAT",
        "CONCATENATE", "CONFIDENCE", "CONFIDENCE.NORM", "CONFIDENCE.T", "CONVERT_OOO", "CORREL",
        "COS", "COSH", "COT", "COTH", "COUNT", "COUNTA",
        "COUNTBLANK", "COUNTIF", "COUNTIFS", "COVAR", "COVARIANCE.P", "COVARIANCE.S",
        "CRITBINOM", "CSC", "CSCH", "CUMIPMT", "CUMPRINC", "CURRENT",
        "DATE", "DATEDIF", "DATEVALUE", "DAVERAGE", "DAY", "DAYS",
        "DAYS360", "DB", "DCOUNT", "DCOUNTA", "DDB", "DDE",
        "DECIMAL", "DEGREES", "DEVSQ", "DGET", "DMAX", "DMIN",
        "DOLLAR", "DPRODUCT", "DROP", "DSTDEV", "DSTDEVP", "DSUM",
        "DVAR", "DVARP", "EASTERSUNDAY", "EFFECT", "ENCODEURL", "ERF.PRECISE",
        "ERFC.PRECISE", "ERROR.TYPE", "ERRORTYPE", "EUROCONVERT", "EVEN", "EXACT",
        "EXP", "EXPAND", "EXPON.DIST", "EXPONDIST", "F.DIST", "F.DIST.RT",
        "F.INV", "F.INV.RT", "F.TEST", "FACT", "FALSE", "FDIST",
        "FILTER", "FILTERXML", "FIND", "FINDB", "FINV", "FISHER",
        "FISHERINV", "FIXED", "FLOOR", "FLOOR.MATH", "FLOOR.PRECISE", "FLOOR.XCL",
        "FORECAST", "FORECAST.ETS.ADD", "FORECAST.ETS.MULT", "FORECAST.ETS.PI.ADD", "FORECAST.ETS.PI.MULT", "FORECAST.ETS.SEASONALITY",
        "FORECAST.ETS.STAT.ADD", "FORECAST.ETS.STAT.MULT", "FORECAST.LINEAR", "FORMULA", "FOURIER", "FREQUENCY",
        "FTEST", "FV", "GAMMA", "GAMMA.DIST", "GAMMA.INV", "GAMMADIST",
        "GAMMAINV", "GAMMALN", "GAMMALN.PRECISE", "GAUSS", "GCD", "GEOMEAN",
        "GETPIVOTDATA", "GOALSEEK", "GROWTH", "HARMEAN", "HLOOKUP", "HOUR",
        "HSTACK", "HYPERLINK", "HYPGEOM.DIST", "HYPGEOMDIST", "IF", "IFERROR",
        "IFNA", "IFS", "INDEX", "INDIRECT", "INFO", "INT",
        "INTERCEPT", "IPMT", "IRR", "ISBLANK", "ISERR", "ISERROR",
        "ISEVEN", "ISFORMULA", "ISLOGICAL", "ISNA", "ISNONTEXT", "ISNUMBER",
        "ISO.CEILING", "ISODD", "ISOMITTED", "ISOWEEKNUM", "ISPMT", "ISREF",
        "ISTEXT", "JIS", "KURT", "LAMBDA", "LARGE", "LCM",
        "LEFT", "LEFTB", "LEN", "LENB", "LET", "LINEST",
        "LN", "LOG", "LOG10", "LOGEST", "LOGINV", "LOGNORM.DIST",
        "LOGNORM.INV", "LOGNORMDIST", "LOOKUP", "LOWER", "MAKEARRAY", "MAP",
        "MATCH", "MAX", "MAXA", "MAXIFS", "MDETERM", "MEDIAN",
        "MID", "MIDB", "MIN", "MINA", "MINIFS", "MINUTE",
        "MINVERSE", "MIRR", "MMULT", "MOD", "MODE", "MODE.MULT",
        "MODE.SNGL", "MONTH", "MULTIPLE.OPERATIONS", "MULTIRANGE", "MUNIT", "MVALUE",
        "N", "NA", "NEG", "NEGBINOM.DIST", "NEGBINOMDIST", "NETWORKDAYS",
        "NETWORKDAYS.INTL", "NOMINAL", "NORM.DIST", "NORM.INV", "NORM.S.DIST", "NORM.S.INV",
        "NORMDIST", "NORMINV", "NORMSDIST", "NORMSINV", "NOT", "NOW",
        "NPER", "NPV", "NUMBERVALUE", "ODD", "OFFSET", "OR",
        "PDURATION", "PEARSON", "PERCENTILE", "PERCENTILE.EXC", "PERCENTILE.INC", "PERCENTRANK",
        "PERCENTRANK.EXC", "PERCENTRANK.INC", "PERMUT", "PERMUTATIONA", "PHI", "PI",
        "PMT", "POISSON", "POISSON.DIST", "POWER", "PPMT", "PROB",
        "PRODUCT", "PROPER", "PV", "QUARTILE", "QUARTILE.EXC", "QUARTILE.INC",
        "RADIANS", "RAND", "RAND.NV", "RANDARRAY", "RANDBETWEEN.NV", "RANK",
        "RANK.AVG", "RANK.EQ", "RATE", "RAWSUBTRACT", "REDUCE", "REGEX",
        "REPLACE", "REPLACEB", "REPT", "RIGHT", "RIGHTB", "ROMAN",
        "ROUND", "ROUNDDOWN", "ROUNDSIG", "ROUNDUP", "ROW", "ROWS",
        "RRI", "RSQ", "SCAN", "SEARCH", "SEARCHB", "SEC",
        "SECH", "SECOND", "SEQUENCE", "SHEET", "SHEETS", "SIGN",
        "SIN", "SINH", "SKEW", "SKEWP", "SLN", "SLOPE",
        "SMALL", "SORT", "SORTBY", "SQRT", "STANDARDIZE", "STDEV",
        "STDEV.P", "STDEV.S", "STDEVA", "STDEVP", "STDEVPA", "STEYX",
        "STYLE", "SUBSTITUTE", "SUBTOTAL", "SUM", "SUMIF", "SUMIFS",
        "SUMPRODUCT", "SUMSQ", "SUMX2MY2", "SUMX2PY2", "SUMXMY2", "SWITCH",
        "SYD", "T", "T.DIST", "T.DIST.2T", "T.DIST.RT", "T.INV",
        "T.INV.2T", "T.TEST", "TAKE", "TAN", "TANH", "TDIST",
        "TEXT", "TEXTAFTER", "TEXTBEFORE", "TEXTJOIN", "TEXTSPLIT", "TIME",
        "TIMEVALUE", "TINV", "TOCOL", "TODAY", "TOROW", "TRANSPOSE",
        "TREND", "TRIM", "TRIMMEAN", "TRUE", "TRUNC", "TTEST",
        "TYPE", "UNICHAR", "UNICODE", "UNIQUE", "UPPER", "VALUE",
        "VAR", "VAR.P", "VAR.S", "VARA", "VARP", "VARPA",
        "VDB", "VLOOKUP", "VSTACK", "WEBSERVICE", "WEEKDAY", "WEEKNUM",
        "WEEKNUM_OOO", "WEIBULL", "WEIBULL.DIST", "WORKDAY.INTL", "WRAPCOLS", "WRAPROWS",
        "XLOOKUP", "XMATCH", "XOR", "YEAR", "Z.TEST", "ZTEST",
    };

    public static readonly IReadOnlySet<string> Analysis = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ACCRINT", "ACCRINTM", "AMORDEGRC", "AMORLINC", "BESSELI", "BESSELJ",
        "BESSELK", "BESSELY", "BIN2DEC", "BIN2HEX", "BIN2OCT", "COMPLEX",
        "CONVERT", "COUPDAYBS", "COUPDAYS", "COUPDAYSNC", "COUPNCD", "COUPNUM",
        "COUPPCD", "CUMIPMT", "CUMPRINC", "DEC2BIN", "DEC2HEX", "DEC2OCT",
        "DELTA", "DISC", "DOLLARDE", "DOLLARFR", "DURATION", "EDATE",
        "EFFECT", "EOMONTH", "ERF", "ERFC", "FACTDOUBLE", "FVSCHEDULE",
        "GCD", "GESTEP", "HEX2BIN", "HEX2DEC", "HEX2OCT", "IMABS",
        "IMAGINARY", "IMARGUMENT", "IMCONJUGATE", "IMCOS", "IMCOSH", "IMCOT",
        "IMCSC", "IMCSCH", "IMDIV", "IMEXP", "IMLN", "IMLOG10",
        "IMLOG2", "IMPOWER", "IMPRODUCT", "IMREAL", "IMSEC", "IMSECH",
        "IMSIN", "IMSINH", "IMSQRT", "IMSUB", "IMSUM", "IMTAN",
        "INTRATE", "ISEVEN", "ISODD", "LCM", "MDURATION", "MROUND",
        "MULTINOMIAL", "NETWORKDAYS", "NOMINAL", "OCT2BIN", "OCT2DEC", "OCT2HEX",
        "ODDFPRICE", "ODDFYIELD", "ODDLPRICE", "ODDLYIELD", "PRICE", "PRICEDISC",
        "PRICEMAT", "QUOTIENT", "RANDBETWEEN", "RECEIVED", "SERIESSUM", "SQRTPI",
        "TBILLEQ", "TBILLPRICE", "TBILLYIELD", "WEEKNUM", "WORKDAY", "XIRR",
        "XNPV", "YEARFRAC", "YIELD", "YIELDDISC", "YIELDMAT",
    };

    public static readonly IReadOnlySet<string> DateAddIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DAYSINMONTH", "DAYSINYEAR", "ISLEAPYEAR", "MONTHS", "ROT13", "WEEKS",
        "WEEKSINYEAR", "YEARS",
    };

    public static readonly IReadOnlySet<string> Pricing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "OPT_BARRIER", "OPT_PROB_HIT", "OPT_PROB_INMONEY", "OPT_TOUCH",
    };

    public static readonly IReadOnlySet<string> Required = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ABS", "ACCRINT", "ACCRINTM", "ACOS", "ACOSH", "ACOT",
        "ACOTH", "ADDRESS", "AGGREGATE", "AMORDEGRC", "AMORLINC", "AND",
        "ARABIC", "AREAS", "ARRAYTOTEXT", "ASC", "ASIN", "ASINH",
        "ATAN", "ATAN2", "ATANH", "AVEDEV", "AVERAGE", "AVERAGEA",
        "AVERAGEIF", "AVERAGEIFS", "B", "BAHTTEXT", "BASE", "BESSELI",
        "BESSELJ", "BESSELK", "BESSELY", "BETA.DIST", "BETA.INV", "BETADIST",
        "BETAINV", "BIN2DEC", "BIN2HEX", "BIN2OCT", "BINOM.DIST", "BINOM.INV",
        "BINOMDIST", "BITAND", "BITLSHIFT", "BITOR", "BITRSHIFT", "BITXOR",
        "BYCOL", "BYROW", "CEILING", "CEILING.MATH", "CEILING.PRECISE", "CEILING.XCL",
        "CELL", "CHAR", "CHIDIST", "CHIINV", "CHISQ.DIST", "CHISQ.DIST.RT",
        "CHISQ.INV", "CHISQ.INV.RT", "CHISQ.TEST", "CHISQDIST", "CHISQINV", "CHITEST",
        "CHOOSE", "CHOOSECOLS", "CHOOSEROWS", "CLEAN", "CODE", "COLOR",
        "COLUMN", "COLUMNS", "COMBIN", "COMBINA", "COMPLEX", "CONCAT",
        "CONCATENATE", "CONFIDENCE", "CONFIDENCE.NORM", "CONFIDENCE.T", "CONVERT", "CONVERT_OOO",
        "CORREL", "COS", "COSH", "COT", "COTH", "COUNT",
        "COUNTA", "COUNTBLANK", "COUNTIF", "COUNTIFS", "COUPDAYBS", "COUPDAYS",
        "COUPDAYSNC", "COUPNCD", "COUPNUM", "COUPPCD", "COVAR", "COVARIANCE.P",
        "COVARIANCE.S", "CRITBINOM", "CSC", "CSCH", "CUMIPMT", "CUMPRINC",
        "CURRENT", "DATE", "DATEDIF", "DATEVALUE", "DAVERAGE", "DAY",
        "DAYS", "DAYS360", "DAYSINMONTH", "DAYSINYEAR", "DB", "DCOUNT",
        "DCOUNTA", "DDB", "DDE", "DEC2BIN", "DEC2HEX", "DEC2OCT",
        "DECIMAL", "DEGREES", "DELTA", "DEVSQ", "DGET", "DISC",
        "DMAX", "DMIN", "DOLLAR", "DOLLARDE", "DOLLARFR", "DPRODUCT",
        "DROP", "DSTDEV", "DSTDEVP", "DSUM", "DURATION", "DVAR",
        "DVARP", "EASTERSUNDAY", "EDATE", "EFFECT", "ENCODEURL", "EOMONTH",
        "ERF", "ERF.PRECISE", "ERFC", "ERFC.PRECISE", "ERROR.TYPE", "ERRORTYPE",
        "EUROCONVERT", "EVEN", "EXACT", "EXP", "EXPAND", "EXPON.DIST",
        "EXPONDIST", "F.DIST", "F.DIST.RT", "F.INV", "F.INV.RT", "F.TEST",
        "FACT", "FACTDOUBLE", "FALSE", "FDIST", "FILTER", "FILTERXML",
        "FIND", "FINDB", "FINV", "FISHER", "FISHERINV", "FIXED",
        "FLOOR", "FLOOR.MATH", "FLOOR.PRECISE", "FLOOR.XCL", "FORECAST", "FORECAST.ETS.ADD",
        "FORECAST.ETS.MULT", "FORECAST.ETS.PI.ADD", "FORECAST.ETS.PI.MULT", "FORECAST.ETS.SEASONALITY", "FORECAST.ETS.STAT.ADD", "FORECAST.ETS.STAT.MULT",
        "FORECAST.LINEAR", "FORMULA", "FOURIER", "FREQUENCY", "FTEST", "FV",
        "FVSCHEDULE", "GAMMA", "GAMMA.DIST", "GAMMA.INV", "GAMMADIST", "GAMMAINV",
        "GAMMALN", "GAMMALN.PRECISE", "GAUSS", "GCD", "GEOMEAN", "GESTEP",
        "GETPIVOTDATA", "GOALSEEK", "GROWTH", "HARMEAN", "HEX2BIN", "HEX2DEC",
        "HEX2OCT", "HLOOKUP", "HOUR", "HSTACK", "HYPERLINK", "HYPGEOM.DIST",
        "HYPGEOMDIST", "IF", "IFERROR", "IFNA", "IFS", "IMABS",
        "IMAGINARY", "IMARGUMENT", "IMCONJUGATE", "IMCOS", "IMCOSH", "IMCOT",
        "IMCSC", "IMCSCH", "IMDIV", "IMEXP", "IMLN", "IMLOG10",
        "IMLOG2", "IMPOWER", "IMPRODUCT", "IMREAL", "IMSEC", "IMSECH",
        "IMSIN", "IMSINH", "IMSQRT", "IMSUB", "IMSUM", "IMTAN",
        "INDEX", "INDIRECT", "INFO", "INT", "INTERCEPT", "INTRATE",
        "IPMT", "IRR", "ISBLANK", "ISERR", "ISERROR", "ISEVEN",
        "ISFORMULA", "ISLEAPYEAR", "ISLOGICAL", "ISNA", "ISNONTEXT", "ISNUMBER",
        "ISO.CEILING", "ISODD", "ISOMITTED", "ISOWEEKNUM", "ISPMT", "ISREF",
        "ISTEXT", "JIS", "KURT", "LAMBDA", "LARGE", "LCM",
        "LEFT", "LEFTB", "LEN", "LENB", "LET", "LINEST",
        "LN", "LOG", "LOG10", "LOGEST", "LOGINV", "LOGNORM.DIST",
        "LOGNORM.INV", "LOGNORMDIST", "LOOKUP", "LOWER", "MAKEARRAY", "MAP",
        "MATCH", "MAX", "MAXA", "MAXIFS", "MDETERM", "MDURATION",
        "MEDIAN", "MID", "MIDB", "MIN", "MINA", "MINIFS",
        "MINUTE", "MINVERSE", "MIRR", "MMULT", "MOD", "MODE",
        "MODE.MULT", "MODE.SNGL", "MONTH", "MONTHS", "MROUND", "MULTINOMIAL",
        "MULTIPLE.OPERATIONS", "MULTIRANGE", "MUNIT", "MVALUE", "N", "NA",
        "NEG", "NEGBINOM.DIST", "NEGBINOMDIST", "NETWORKDAYS", "NETWORKDAYS.INTL", "NOMINAL",
        "NORM.DIST", "NORM.INV", "NORM.S.DIST", "NORM.S.INV", "NORMDIST", "NORMINV",
        "NORMSDIST", "NORMSINV", "NOT", "NOW", "NPER", "NPV",
        "NUMBERVALUE", "OCT2BIN", "OCT2DEC", "OCT2HEX", "ODD", "ODDFPRICE",
        "ODDFYIELD", "ODDLPRICE", "ODDLYIELD", "OFFSET", "OPT_BARRIER", "OPT_PROB_HIT",
        "OPT_PROB_INMONEY", "OPT_TOUCH", "OR", "PDURATION", "PEARSON", "PERCENTILE",
        "PERCENTILE.EXC", "PERCENTILE.INC", "PERCENTRANK", "PERCENTRANK.EXC", "PERCENTRANK.INC", "PERMUT",
        "PERMUTATIONA", "PHI", "PI", "PMT", "POISSON", "POISSON.DIST",
        "POWER", "PPMT", "PRICE", "PRICEDISC", "PRICEMAT", "PROB",
        "PRODUCT", "PROPER", "PV", "QUARTILE", "QUARTILE.EXC", "QUARTILE.INC",
        "QUOTIENT", "RADIANS", "RAND", "RAND.NV", "RANDARRAY", "RANDBETWEEN",
        "RANDBETWEEN.NV", "RANK", "RANK.AVG", "RANK.EQ", "RATE", "RAWSUBTRACT",
        "RECEIVED", "REDUCE", "REGEX", "REGEXEXTRACT", "REGEXREPLACE", "REGEXTEST", "REPLACE", "REPLACEB", "REPT",
        "RIGHT", "RIGHTB", "ROMAN", "ROT13", "ROUND", "ROUNDDOWN",
        "ROUNDSIG", "ROUNDUP", "ROW", "ROWS", "RRI", "RSQ",
        "SCAN", "SEARCH", "SEARCHB", "SEC", "SECH", "SECOND",
        "SEQUENCE", "SERIESSUM", "SHEET", "SHEETS", "SIGN", "SIN",
        "SINH", "SKEW", "SKEWP", "SLN", "SLOPE", "SMALL",
        "SORT", "SORTBY", "SQRT", "SQRTPI", "STANDARDIZE", "STDEV",
        "STDEV.P", "STDEV.S", "STDEVA", "STDEVP", "STDEVPA", "STEYX",
        "STYLE", "SUBSTITUTE", "SUBTOTAL", "SUM", "SUMIF", "SUMIFS",
        "SUMPRODUCT", "SUMSQ", "SUMX2MY2", "SUMX2PY2", "SUMXMY2", "SWITCH",
        "SYD", "T", "T.DIST", "T.DIST.2T", "T.DIST.RT", "T.INV",
        "T.INV.2T", "T.TEST", "TAKE", "TAN", "TANH", "TBILLEQ",
        "TBILLPRICE", "TBILLYIELD", "TDIST", "TEXT", "TEXTAFTER", "TEXTBEFORE",
        "TEXTJOIN", "TEXTSPLIT", "TIME", "TIMEVALUE", "TINV", "TOCOL",
        "TODAY", "TOROW", "TRANSPOSE", "TREND", "TRIM", "TRIMMEAN",
        "TRUE", "TRUNC", "TTEST", "TYPE", "UNICHAR", "UNICODE",
        "UNIQUE", "UPPER", "VALUE", "VAR", "VAR.P", "VAR.S",
        "VARA", "VARP", "VARPA", "VDB", "VLOOKUP", "VSTACK",
        "WEBSERVICE", "WEEKDAY", "WEEKNUM", "WEEKNUM_OOO", "WEEKS", "WEEKSINYEAR",
        "WEIBULL", "WEIBULL.DIST", "WORKDAY", "WORKDAY.INTL", "WRAPCOLS", "WRAPROWS",
        "XIRR", "XLOOKUP", "XMATCH", "XNPV", "XOR", "YEAR",
        "YEARFRAC", "YEARS", "YIELD", "YIELDDISC", "YIELDMAT", "Z.TEST",
        "ZTEST",
    };

    public static IReadOnlySet<string> Implemented(
    IFunctionRegistry registry, ISpecialFormRegistry? specialForms = null)
    {
        var names = new HashSet<string>(registry.All.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
        if (specialForms is not null)
            names.UnionWith(specialForms.All.Select(f => f.Name));
        return names;
    }

    public static IReadOnlyList<string> Missing(
    IFunctionRegistry registry, ISpecialFormRegistry? specialForms = null)
    {
        var implemented = Implemented(registry, specialForms);
        return [.. Required.Where(name => !implemented.Contains(name)).OrderBy(n => n, StringComparer.Ordinal)];
    }

    public static IReadOnlyList<string> Unexpected(
    IFunctionRegistry registry, ISpecialFormRegistry? specialForms = null) =>
    [.. Implemented(registry, specialForms).Where(n => !Required.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)];

    public static double CoverageFraction(
        IFunctionRegistry registry, ISpecialFormRegistry? specialForms = null) =>
        Required.Count == 0
            ? 1
            : (Required.Count - Missing(registry, specialForms).Count) / (double)Required.Count;
}
