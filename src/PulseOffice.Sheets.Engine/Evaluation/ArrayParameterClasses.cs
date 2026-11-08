namespace PulseOffice.Sheets.Engine.Evaluation;

public sealed class ArrayParameterClasses
{
    private static readonly int[] AllParameters = [];

    private readonly Dictionary<string, int[]> _arrayParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SUMPRODUCT"] = AllParameters, ["MMULT"] = AllParameters, ["MINVERSE"] = AllParameters, ["MDETERM"] = AllParameters,
        ["TRANSPOSE"] = AllParameters, ["FREQUENCY"] = AllParameters, ["LINEST"] = [0, 1], ["LOGEST"] = [0, 1],
        ["TREND"] = [0, 1, 2], ["GROWTH"] = [0, 1, 2], ["CORREL"] = AllParameters, ["COVAR"] = AllParameters,
        ["COVARIANCE.P"] = AllParameters, ["COVARIANCE.S"] = AllParameters, ["PEARSON"] = AllParameters, ["RSQ"] = AllParameters,
        ["SLOPE"] = AllParameters, ["INTERCEPT"] = AllParameters, ["STEYX"] = AllParameters, ["FORECAST"] = [1, 2],
        ["FORECAST.LINEAR"] = [1, 2], ["FTEST"] = AllParameters, ["F.TEST"] = AllParameters, ["TTEST"] = [0, 1], ["T.TEST"] = [0, 1],
        ["PROB"] = [0, 1], ["SUMX2MY2"] = AllParameters, ["SUMX2PY2"] = AllParameters, ["SUMXMY2"] = AllParameters,
        ["MODE"] = AllParameters, ["MODE.SNGL"] = AllParameters, ["MODE.MULT"] = AllParameters, ["CHITEST"] = AllParameters,
        ["CHISQ.TEST"] = AllParameters, ["FOURIER"] = [0], ["LOOKUP"] = [1, 2], ["MATCH"] = [1], ["VLOOKUP"] = [1],
        ["HLOOKUP"] = [1], ["XLOOKUP"] = [1, 2], ["XMATCH"] = [1], ["FILTER"] = [0, 1], ["SORT"] = [0, 1, 2],
        ["SORTBY"] = AllParameters, ["UNIQUE"] = [0], ["AGGREGATE"] = [2], ["BYROW"] = [0], ["BYCOL"] = [0],
        ["MAP"] = AllParameters, ["REDUCE"] = [1], ["SCAN"] = [1], ["CHOOSECOLS"] = [0], ["CHOOSEROWS"] = [0],
        ["DROP"] = [0], ["TAKE"] = [0], ["EXPAND"] = [0], ["HSTACK"] = AllParameters, ["VSTACK"] = AllParameters,
        ["TOCOL"] = [0], ["TOROW"] = [0], ["WRAPROWS"] = [0], ["WRAPCOLS"] = [0], ["TEXTSPLIT"] = [0, 1, 2],
        ["FORECAST.ETS.ADD"] = [0, 1, 2], ["FORECAST.ETS.MULT"] = [0, 1, 2], ["FORECAST.ETS.PI.ADD"] = [0, 1, 2],
        ["FORECAST.ETS.PI.MULT"] = [0, 1, 2], ["FORECAST.ETS.SEASONALITY"] = [0, 1], ["FORECAST.ETS.STAT.ADD"] = [0, 1, 2],
        ["FORECAST.ETS.STAT.MULT"] = [0, 1, 2],
    };

    public bool IsArrayParameter(string function, int index) =>
        _arrayParameters.TryGetValue(function, out var parameters) && (parameters.Length == 0 || parameters.Contains(index));
}
