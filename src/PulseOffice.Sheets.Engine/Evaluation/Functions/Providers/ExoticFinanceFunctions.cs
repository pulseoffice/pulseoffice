using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ExoticFinanceFunctions(IDateSystem dates) : IFunctionProvider
{
    public string Category => "Finanza";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        RegisterOptions(builder);
        RegisterOddCouponBonds(builder);

        return builder.Build();
    }

    private static void RegisterOptions(FunctionBuilder builder)
    {
        builder.Raw("OPT_PROB_HIT", 5, 5, r =>
        {
            var spot = r.Number(0);
            var volatility = r.Number(1);
            var drift = r.Number(2);
            var time = r.Number(3);
            var barrier = r.Number(4);
            if (r.HasError) return r.Error;

            var probability = OptionPricing.ProbabilityOfHitting(spot, volatility, drift, time, barrier);
            return double.IsNaN(probability) ? F.OutOfDomain() : F.Number(probability);
        });

        builder.Raw("OPT_PROB_INMONEY", 5, 7, r =>
        {
            var spot = r.Number(0);
            var volatility = r.Number(1);
            var drift = r.Number(2);
            var time = r.Number(3);
            var lower = r.Number(4, 0);
            var upper = r.Number(5, 0);
            if (r.HasError) return r.Error;

            var probability = OptionPricing.ProbabilityInMoney(spot, volatility, drift, time, lower, upper);
            return double.IsNaN(probability) ? F.OutOfDomain() : F.Number(probability);
        });

        builder.Raw("OPT_TOUCH", 7, 8, r =>
        {
            var spot = r.Number(0);
            var volatility = r.Number(1);
            var rate = r.Number(2);
            var foreignRate = r.Number(3);
            var time = r.Number(4);
            var barrier = r.Number(5);

            var payAtHit = r.Text(7, "no").StartsWith('y');
            if (r.HasError) return r.Error;

            var value = OptionPricing.TouchValue(spot, volatility, rate, foreignRate, time, barrier, payAtHit);
            return double.IsNaN(value) ? F.OutOfDomain() : F.Number(value);
        });

        builder.Raw("OPT_BARRIER", 9, 10, r =>
        {
            var spot = r.Number(0);
            var volatility = r.Number(1);
            var rate = r.Number(2);
            var foreignRate = r.Number(3);
            var time = r.Number(4);
            var strikeLow = r.Number(5);
            var strikeHigh = r.Number(6);
            var barrierLow = r.Number(7);
            var barrierHigh = r.Number(8);
            var call = !r.Text(9, "c").StartsWith('p');
            if (r.HasError) return r.Error;

            var value = OptionPricing.BarrierValue(
                spot, volatility, rate, foreignRate, time,
                strikeLow, strikeHigh, barrierLow, barrierHigh, call);

            return double.IsNaN(value) ? F.OutOfDomain() : F.Number(value);
        });
    }

    private void RegisterOddCouponBonds(FunctionBuilder builder)
    {
        builder.Raw("ODDFPRICE", 8, 9, r => OddCoupon(r, first: true, wantPrice: true));
        builder.Raw("ODDFYIELD", 8, 9, r => OddCoupon(r, first: true, wantPrice: false));

        builder.Raw("ODDLPRICE", 7, 8, r => OddCoupon(r, first: false, wantPrice: true));
        builder.Raw("ODDLYIELD", 7, 8, r => OddCoupon(r, first: false, wantPrice: false));
    }

    private CellValue OddCoupon(ArgumentReader r, bool first, bool wantPrice)
    {
        var settlement = Date(r, 0);
        var maturity = Date(r, 1);
        var anchor = Date(r, 2);
        var firstCoupon = first ? Date(r, 3) : anchor;
        var rateIndex = first ? 4 : 3;

        var rate = r.Number(rateIndex);
        var priceOrYield = r.Number(rateIndex + 1);
        var redemption = r.Number(rateIndex + 2);
        var frequency = r.Integer(rateIndex + 3);
        var basis = r.Integer(rateIndex + 4, 0);
        if (r.HasError) return r.Error;
        if (frequency is not (1 or 2 or 4) || basis is < 0 or > 4 || rate < 0 || priceOrYield <= 0 && !(wantPrice && priceOrYield == 0) || redemption <= 0)
            return F.OutOfDomain();

        double? Price(double yield) => first
            ? OddCouponMath.FirstPeriodPrice(settlement, maturity, anchor, firstCoupon, rate, yield, redemption, frequency, basis)
            : OddCouponMath.LastPeriodPrice(settlement, maturity, anchor, rate, yield, redemption, frequency, basis);

        if (wantPrice) return Price(priceOrYield) is { } price ? F.Number(price) : F.OutOfDomain();
        if (!first)
            return OddCouponMath.LastPeriodYield(settlement, maturity, anchor, rate, priceOrYield, redemption, frequency, basis) is { } yield
                ? F.Number(yield) : F.OutOfDomain();
        if (Price(0) is null) return F.OutOfDomain();

        var solved = Solve(yield => Price(yield)!.Value - priceOrYield);
        return solved is { } value ? F.Number(value) : F.OutOfDomain();
    }

    private DateTime Date(ArgumentReader r, int index) => dates.FromSerial(r.Number(index));

    private static double? Solve(Func<double, double> f)
    {
        double low = -0.99, high = 10.0;
        var valueLow = f(low);
        if (valueLow * f(high) > 0) return null;

        for (var i = 0; i < 200; i++)
        {
            var middle = (low + high) / 2;
            var value = f(middle);
            if (M.Abs(value) < 1e-10 || high - low < 1e-12) return middle;

            if (valueLow * value <= 0) high = middle;
            else { low = middle; valueLow = value; }
        }
        return (low + high) / 2;
    }
}
