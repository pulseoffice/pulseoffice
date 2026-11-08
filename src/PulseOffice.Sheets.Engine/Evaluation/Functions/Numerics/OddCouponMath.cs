using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class OddCouponMath
{
    public static double? FirstPeriodPrice(
        DateTime settlement, DateTime maturity, DateTime issue, DateTime firstCoupon,
        double rate, double yield, double redemption, int frequency, int basis)
    {
        if (!(maturity > firstCoupon && firstCoupon > settlement && settlement > issue)) return null;
        var months = 12 / frequency;
        var coupon = 100 * rate / frequency;
        var growth = 1 + yield / frequency;
        var later = CouponsAfter(firstCoupon, maturity, months);

        var quasi = new List<DateTime> { firstCoupon };
        while (quasi[^1] > issue) quasi.Add(firstCoupon.AddMonths(-months * quasi.Count));
        quasi.Reverse();

        var settlementPeriod = Enumerable.Range(1, quasi.Count - 1).First(i => quasi[i] > settlement);
        var periodLength = PeriodLength(quasi[settlementPeriod - 1], quasi[settlementPeriod], frequency, basis);
        var toNextQuasi = Days(settlement, quasi[settlementPeriod], basis) / periodLength;
        var wholeQuasiAfter = quasi.Count - 1 - settlementPeriod;

        double counted = 0, accrued = 0;
        for (var i = 1; i < quasi.Count; i++)
        {
            var length = PeriodLength(quasi[i - 1], quasi[i], frequency, basis);
            var start = quasi[i - 1] > issue ? quasi[i - 1] : issue;
            counted += Days(start, quasi[i], basis) / length;
            var accrualEnd = quasi[i] < settlement ? quasi[i] : settlement;
            if (accrualEnd > start) accrued += Days(start, accrualEnd, basis) / length;
        }

        var exponent = wholeQuasiAfter + toNextQuasi;
        var price = redemption / M.Pow(growth, later + exponent) + coupon * counted / M.Pow(growth, exponent) - coupon * accrued;
        for (var k = 1; k <= later; k++) price += coupon / M.Pow(growth, k + exponent);
        return price;
    }

    public static double? LastPeriodPrice(
        DateTime settlement, DateTime maturity, DateTime lastCoupon,
        double rate, double yield, double redemption, int frequency, int basis)
    {
        if (!(maturity > settlement && settlement > lastCoupon)) return null;
        if (Fractions(settlement, maturity, lastCoupon, frequency, basis) is not var (counted, remaining, accrued)) return null;
        return (redemption + counted * 100 * rate / frequency) / (remaining * yield / frequency + 1) - accrued * 100 * rate / frequency;
    }

    public static double? LastPeriodYield(
        DateTime settlement, DateTime maturity, DateTime lastCoupon,
        double rate, double price, double redemption, int frequency, int basis)
    {
        if (!(maturity > settlement && settlement > lastCoupon)) return null;
        if (Fractions(settlement, maturity, lastCoupon, frequency, basis) is not var (counted, remaining, accrued) || remaining == 0) return null;
        var growth = (redemption + counted * 100 * rate / frequency) / (price + accrued * 100 * rate / frequency) - 1;
        return growth * frequency / remaining;
    }

    private static (double Counted, double Remaining, double Accrued)? Fractions(
        DateTime settlement, DateTime maturity, DateTime lastCoupon, int frequency, int basis)
    {
        if (DayCount.YearFraction(lastCoupon, maturity, basis) is not { } counted
            || DayCount.YearFraction(settlement, maturity, basis) is not { } remaining
            || DayCount.YearFraction(lastCoupon, settlement, basis) is not { } accrued) return null;
        return (counted * frequency, remaining * frequency, accrued * frequency);
    }

    private static int CouponsAfter(DateTime firstCoupon, DateTime maturity, int months)
    {
        var count = 0;
        while (firstCoupon.AddMonths(months * (count + 1)) <= maturity) count++;
        return firstCoupon.AddMonths(months * count) < maturity ? count + 1 : count;
    }

    private static double PeriodLength(DateTime start, DateTime end, int frequency, int basis) => basis switch
    {
        1 => (end - start).TotalDays,
        3 => 365.0 / frequency,
        _ => 360.0 / frequency,
    };

    private static double Days(DateTime start, DateTime end, int basis) => DayCount.Days(start, end, basis) ?? (end - start).TotalDays;
}
