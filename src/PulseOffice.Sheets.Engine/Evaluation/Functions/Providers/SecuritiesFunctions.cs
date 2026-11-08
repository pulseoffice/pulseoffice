using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class SecuritiesFunctions(IDateSystem dates) : IFunctionProvider
{
    public string Category => "Finanza";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        RegisterCouponDates(builder);
        RegisterDiscountSecurities(builder);
        RegisterCouponSecurities(builder);
        RegisterTreasuryBills(builder);
        RegisterDepreciation(builder);

        return builder.Build();
    }

    private void RegisterCouponDates(FunctionBuilder builder)
    {
        (string Name, Func<CouponSchedule, double> Select)[] measures =
        [
            ("COUPNUM", schedule => schedule.RemainingCoupons),
            ("COUPDAYS", schedule => schedule.DaysInPeriod),
            ("COUPDAYBS", schedule => schedule.DaysFromPreviousCoupon),
            ("COUPDAYSNC", schedule => schedule.DaysToNextCoupon),
        ];

        foreach (var (name, select) in measures)
            builder.Raw(name, 3, 4, r =>
            {
                var schedule = BuildSchedule(r);
                return schedule is null ? Failure(r) : F.Number(select(schedule.Value));
            });

        builder.Raw("COUPNCD", 3, 4, r =>
        {
            var schedule = BuildSchedule(r);
            return schedule is null ? Failure(r) : F.Number(M.Floor(dates.ToSerial(schedule.Value.NextCoupon)));
        });

        builder.Raw("COUPPCD", 3, 4, r =>
        {
            var schedule = BuildSchedule(r);
            return schedule is null ? Failure(r) : F.Number(M.Floor(dates.ToSerial(schedule.Value.PreviousCoupon)));
        });
    }

    private void RegisterDiscountSecurities(FunctionBuilder builder)
    {
        builder.Raw("DISC", 4, 5, r =>
        {
            var (settlement, maturity) = Period(r);
            var price = r.Number(2);
            var redemption = r.Number(3);
            var basis = r.Integer(4, 0);
            if (r.HasError) return r.Error;

            var fraction = Span(settlement, maturity, basis);
            if (fraction is null or 0 || price <= 0 || redemption <= 0) return F.OutOfDomain();

            return F.Number((redemption - price) / redemption / fraction.Value);
        });

        builder.Raw("PRICEDISC", 4, 5, r =>
        {
            var (settlement, maturity) = Period(r);
            var discount = r.Number(2);
            var redemption = r.Number(3);
            var basis = r.Integer(4, 0);
            if (r.HasError) return r.Error;

            var fraction = DayCount.SecuritySpan(settlement, maturity, basis);
            return fraction is null ? F.OutOfDomain()
                : F.Number(redemption - discount * redemption * fraction.Value);
        });

        builder.Raw("YIELDDISC", 4, 5, r =>
        {
            var (settlement, maturity) = Period(r);
            var price = r.Number(2);
            var redemption = r.Number(3);
            var basis = r.Integer(4, 0);
            if (r.HasError) return r.Error;

            var fraction = Span(settlement, maturity, basis);
            if (fraction is null or 0 || price == 0) return F.OutOfDomain();

            return F.Number((redemption - price) / price / fraction.Value);
        });

        builder.Raw("INTRATE", 4, 5, r =>
        {
            var (settlement, maturity) = Period(r);
            var investment = r.Number(2);
            var redemption = r.Number(3);
            var basis = r.Integer(4, 0);
            if (r.HasError) return r.Error;

            var fraction = DayCount.SecuritySpan(settlement, maturity, basis);
            if (fraction is null or 0 || investment == 0) return F.OutOfDomain();

            return F.Number((redemption - investment) / investment / fraction.Value);
        });

        builder.Raw("RECEIVED", 4, 5, r =>
        {
            var (settlement, maturity) = Period(r);
            var investment = r.Number(2);
            var discount = r.Number(3);
            var basis = r.Integer(4, 0);
            if (r.HasError) return r.Error;

            var fraction = DayCount.SecuritySpan(settlement, maturity, basis);
            if (fraction is null) return F.OutOfDomain();

            var denominator = 1 - discount * fraction.Value;
            return denominator == 0 ? F.DivideByZero() : F.Number(investment / denominator);
        });

        builder.Raw("ACCRINTM", 4, 5, r =>
        {
            var issue = Date(r, 0);
            var maturity = Date(r, 1);
            var rate = r.Number(2);
            var par = r.Number(3, 1000);
            var basis = r.Integer(4, 0);
            if (r.HasError) return r.Error;
            if (rate <= 0 || par <= 0) return F.OutOfDomain();

            var fraction = DayCount.SecuritySpan(issue, maturity, basis);
            return fraction is null ? F.OutOfDomain() : F.Number(par * rate * fraction.Value);
        });

        builder.Raw("ACCRINT", 6, 8, r =>
        {
            var issue = Date(r, 0);
            var settlement = Date(r, 2);
            var rate = r.Number(3);
            var par = r.Number(4);
            var basis = r.Integer(6, 0);
            if (r.HasError) return r.Error;

            var fraction = DayCount.SecuritySpan(issue, settlement, basis);
            return fraction is null ? F.OutOfDomain() : F.Number(par * rate * fraction.Value);
        });

        builder.Raw("PRICEMAT", 5, 6, r =>
        {
            var settlement = Date(r, 0);
            var maturity = Date(r, 1);
            var issue = Date(r, 2);
            var rate = r.Number(3);
            var yield = r.Number(4);
            var basis = r.Integer(5, 0);
            if (r.HasError) return r.Error;

            var issueToMaturity = Span(issue, maturity, basis);
            var issueToSettlement = Span(issue, settlement, basis);
            var settlementToMaturity = Span(settlement, maturity, basis);
            if (issueToMaturity is null || issueToSettlement is null || settlementToMaturity is null)
                return F.OutOfDomain();

            var denominator = 1 + settlementToMaturity.Value * yield;
            if (denominator == 0) return F.DivideByZero();

            return F.Number((100 + issueToMaturity.Value * rate * 100) / denominator
                            - issueToSettlement.Value * rate * 100);
        });

        builder.Raw("YIELDMAT", 5, 6, r =>
        {
            var settlement = Date(r, 0);
            var maturity = Date(r, 1);
            var issue = Date(r, 2);
            var rate = r.Number(3);
            var price = r.Number(4);
            var basis = r.Integer(5, 0);
            if (r.HasError) return r.Error;

            var issueToMaturity = Span(issue, maturity, basis);
            var issueToSettlement = Span(issue, settlement, basis);
            var settlementToMaturity = Span(settlement, maturity, basis);
            if (issueToMaturity is null || issueToSettlement is null || settlementToMaturity is null or 0)
                return F.OutOfDomain();

            var accrued = issueToSettlement.Value * rate;
            var redemption = 1 + issueToMaturity.Value * rate;
            var invested = price / 100 + accrued;
            if (invested == 0) return F.DivideByZero();

            return F.Number((redemption - invested) / invested / settlementToMaturity.Value);
        });
    }

    private void RegisterCouponSecurities(FunctionBuilder builder)
    {
        builder.Raw("PRICE", 6, 7, r =>
        {
            var bond = BondParameters(r);
            if (bond is null) return Failure(r);
            if (bond.Value.Rate < 0 || bond.Value.YieldOrPrice < 0 || bond.Value.Redemption <= 0) return F.OutOfDomain();

            return F.Number(BondPrice(bond.Value, bond.Value.YieldOrPrice));
        });

        builder.Raw("YIELD", 6, 7, r =>
        {
            var bond = BondParameters(r);
            if (bond is null) return Failure(r);
            if (bond.Value.Rate < 0 || bond.Value.YieldOrPrice <= 0 || bond.Value.Redemption <= 0) return F.OutOfDomain();

            return BondYield(bond.Value) is { } value ? F.Number(value) : F.OutOfDomain();
        });

        foreach (var (name, modified) in ((string, bool)[])[("DURATION", false), ("MDURATION", true)])
            builder.Raw(name, 5, 6, r =>
            {
                var settlement = Date(r, 0);
                var maturity = Date(r, 1);
                var rate = r.Number(2);
                var yield = r.Number(3);
                var frequency = r.Integer(4);
                var basis = r.Integer(5, 0);
                if (r.HasError) return r.Error;
                if (rate < 0 || yield < 0) return F.OutOfDomain();

                var schedule = Schedule(settlement, maturity, frequency, basis);
                var years = Span(settlement, maturity, basis);
                if (schedule is null || years is null) return F.OutOfDomain();

                var duration = MacaulayDuration(schedule.Value, years.Value, rate, yield, frequency);
                return F.Number(modified ? duration / (1 + yield / frequency) : duration);
            });
    }

    private void RegisterTreasuryBills(FunctionBuilder builder)
    {
        builder.Raw("TBILLEQ", 3, 3, r =>
        {
            var (settlement, maturity) = Period(r);
            var discount = r.Number(2);
            if (r.HasError) return r.Error;

            var days = (maturity - settlement).TotalDays;
            if (days <= 0 || days > 360) return F.OutOfDomain();

            var denominator = 360 - discount * days;
            return denominator == 0 ? F.DivideByZero() : F.Number(365 * discount / denominator);
        });

        builder.Raw("TBILLPRICE", 3, 3, r =>
        {
            var (settlement, maturity) = Period(r);
            var discount = r.Number(2);
            if (r.HasError) return r.Error;

            var days = (maturity - settlement).TotalDays;
            if (days <= 0 || days > 360) return F.OutOfDomain();

            return F.Number(100 * (1 - discount * days / 360));
        });

        builder.Raw("TBILLYIELD", 3, 3, r =>
        {
            var (settlement, maturity) = Period(r);
            var price = r.Number(2);
            if (r.HasError) return r.Error;

            var days = (maturity - settlement).TotalDays;
            if (days <= 0 || price <= 0) return F.OutOfDomain();

            return F.Number((100 - price) / price * 360 / days);
        });
    }

    private void RegisterDepreciation(FunctionBuilder builder)
    {
        builder.Raw("DB", 4, 5, r =>
        {
            var cost = r.Number(0);
            var salvage = r.Number(1);
            var life = r.Number(2);
            var period = r.Number(3);
            var months = r.Number(4, 12);
            if (r.HasError) return r.Error;
            if (life <= 0 || cost <= 0 || salvage < 0 || period < 1 || period > life + 1 || months is < 1 or > 12) return F.OutOfDomain();

            var rate = M.Round(1 - M.Pow(salvage / cost, 1 / life), 3);

            var accumulated = cost * rate * months / 12;
            if (period == 1) return F.Number(accumulated);

            var last = M.Floor(period);
            var amount = (cost - accumulated) * M.Pow(1 - rate, last - 2) * rate;
            if (last == M.Floor(life) + 1) amount = amount * (12 - months) / 12;
            return F.Number(amount);
        });

        builder.Raw("VDB", 5, 7, r =>
        {
            var cost = r.Number(0);
            var salvage = r.Number(1);
            var life = r.Number(2);
            var start = r.Number(3);
            var end = r.Number(4);
            var factor = r.Number(5, 2);
            var noSwitch = r.Boolean(6, false);
            if (r.HasError) return r.Error;
            if (start < 0 || end < start || end > life || cost < 0 || salvage > cost || factor <= 0) return F.OutOfDomain();
            return F.Number(noSwitch
                ? FinancialMath.VariableDecliningBalanceWithoutSwitch(cost, salvage, life, start, end, factor)
                : FinancialMath.VariableDecliningBalance(cost, salvage, life, start, end, factor));
        });

        foreach (var (name, degressive) in ((string, bool)[])[("AMORLINC", false), ("AMORDEGRC", true)])
            builder.Raw(name, 6, 7, r =>
            {
                var cost = r.Number(0);
                var purchased = Date(r, 1);
                var firstPeriod = Date(r, 2);
                var salvage = r.Number(3);
                var period = r.Number(4);
                var rate = r.Number(5);
                var basis = r.Integer(6, 0);
                if (r.HasError) return r.Error;
                if (rate <= 0 || period < 0 || salvage < 0 || cost < salvage) return F.OutOfDomain();
                if (DayCount.YearFraction(purchased <= firstPeriod ? purchased : firstPeriod, purchased <= firstPeriod ? firstPeriod : purchased, basis) is not { } fraction)
                    return F.OutOfDomain();

                return F.Number(degressive
                    ? DegressiveDepreciation(cost, fraction, salvage, (int)period, rate)
                    : LinearDepreciation(cost, fraction, salvage, (int)period, rate));
            });
    }

    private static double LinearDepreciation(double cost, double firstFraction, double salvage, int period, double rate)
    {
        var fullRate = cost * rate;
        var firstRate = firstFraction * rate * cost;
        var fullPeriods = (int)((cost - salvage - firstRate) / fullRate);
        var result = period == 0 ? firstRate
            : period <= fullPeriods ? fullRate
            : period == fullPeriods + 1 ? cost - salvage - fullRate * fullPeriods - firstRate
            : 0;
        return M.Max(result, 0);
    }

    private static double DegressiveDepreciation(double cost, double firstFraction, double salvage, int period, double rate)
    {
        var life = 1 / rate;
        var coefficient = life switch
        {
            < 3 => 1.0,
            < 5 => 1.5,
            <= 6 => 2.0,
            _ => 2.5,
        };
        rate *= coefficient;
        var depreciation = M.Round(firstFraction * rate * cost, MidpointRounding.AwayFromZero);
        cost -= depreciation;
        var rest = cost - salvage;

        for (var n = 0; n < period; n++)
        {
            depreciation = M.Round(rate * cost, MidpointRounding.AwayFromZero);
            if (depreciation == 0) return 0;
            rest -= depreciation;
            if (rest < 0) return period - n <= 1 ? M.Round(cost * 0.5, MidpointRounding.AwayFromZero) : 0;
            cost -= depreciation;
        }
        return depreciation;
    }

    private readonly record struct CouponSchedule(
    DateTime PreviousCoupon, DateTime NextCoupon,
    double DaysInPeriod, double DaysFromPreviousCoupon, double DaysToNextCoupon,
    int RemainingCoupons);

    private CouponSchedule? BuildSchedule(ArgumentReader r)
    {
        var settlement = Date(r, 0);
        var maturity = Date(r, 1);
        var frequency = r.Integer(2);
        var basis = r.Integer(3, 0);
        return r.HasError ? null : Schedule(settlement, maturity, frequency, basis);
    }

    private static CouponSchedule? Schedule(DateTime settlement, DateTime maturity, int frequency, int basis)
    {
        if (frequency is not (1 or 2 or 4) || basis is < 0 or > 4 || settlement >= maturity) return null;

        var monthsPerCoupon = 12 / frequency;
        var endOfMonth = maturity.Day == DateTime.DaysInMonth(maturity.Year, maturity.Month);
        DateTime CouponBefore(int count)
        {
            var date = maturity.AddMonths(-count * monthsPerCoupon);
            return endOfMonth ? new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)) : date;
        }

        var coupons = 1;
        while (CouponBefore(coupons) > settlement) coupons++;
        var previous = CouponBefore(coupons);
        var next = CouponBefore(coupons - 1);

        var daysInPeriod = basis switch
        {
            1 => (next - previous).TotalDays,
            3 => 365.0 / frequency,
            _ => 360.0 / frequency,
        };
        var daysFromPrevious = DayCount.Days(previous, settlement, basis) ?? 0;
        var daysToNext = basis is 0 or 4
            ? daysInPeriod - daysFromPrevious
            : DayCount.Days(settlement, next, basis) ?? 0;

        return new CouponSchedule(previous, next, daysInPeriod, daysFromPrevious, daysToNext, coupons);
    }

    private readonly record struct Bond(
    CouponSchedule Schedule, double Rate, double YieldOrPrice, double Redemption, int Frequency);

    private Bond? BondParameters(ArgumentReader r)
    {
        var settlement = Date(r, 0);
        var maturity = Date(r, 1);
        var rate = r.Number(2);
        var yieldOrPrice = r.Number(3);
        var redemption = r.Number(4);
        var frequency = r.Integer(5);
        var basis = r.Integer(6, 0);
        if (r.HasError) return null;

        var schedule = Schedule(settlement, maturity, frequency, basis);
        return schedule is null ? null : new Bond(schedule.Value, rate, yieldOrPrice, redemption, frequency);
    }

    private static double BondPrice(Bond bond, double yield)
    {
        var schedule = bond.Schedule;
        var frequency = (double)bond.Frequency;
        var toNext = schedule.DaysToNextCoupon / schedule.DaysInPeriod;
        var coupons = schedule.RemainingCoupons;
        var coupon = 100 * bond.Rate / frequency;
        var growth = 1 + yield / frequency;

        var price = bond.Redemption / M.Pow(growth, coupons - 1 + toNext)
                  - coupon * schedule.DaysFromPreviousCoupon / schedule.DaysInPeriod;
        for (var k = 0; k < coupons; k++)
            price += coupon / M.Pow(growth, k + toNext);
        return price;
    }

    private static double? BondYield(Bond bond)
    {
        var target = bond.YieldOrPrice;
        double low = 0, high = 1;
        var priceLow = BondPrice(bond, low);
        var priceHigh = BondPrice(bond, high);
        var guess = (high - low) / 2;
        var priceGuess = double.NaN;

        for (var iteration = 0; iteration < 100; iteration++)
        {
            priceGuess = BondPrice(bond, guess);
            if (SpreadsheetMath.ApproxEqual(target, priceLow)) return low;
            if (SpreadsheetMath.ApproxEqual(target, priceHigh)) return high;
            if (SpreadsheetMath.ApproxEqual(target, priceGuess)) return guess;

            if (target < priceHigh)
            {
                high *= 2;
                priceHigh = BondPrice(bond, high);
                guess = (high - low) / 2;
            }
            else
            {
                if (target < priceGuess) (low, priceLow) = (guess, priceGuess);
                else (high, priceHigh) = (guess, priceGuess);
                guess = high - (high - low) * ((target - priceHigh) / (priceLow - priceHigh));
            }
        }
        return M.Abs(target - priceGuess) > target / 100 ? null : guess;
    }

    private static double MacaulayDuration(CouponSchedule schedule, double years, double rate, double yield, int frequency)
    {
        var coupons = (double)schedule.RemainingCoupons;
        var coupon = rate * 100 / frequency;
        var growth = 1 + yield / frequency;
        var shift = years * frequency - coupons;

        double weighted = 0, price = 0;
        for (var t = 1.0; t < coupons; t++)
        {
            var present = coupon / M.Pow(growth, t + shift);
            weighted += (t + shift) * present;
            price += present;
        }
        var last = (coupon + 100) / M.Pow(growth, coupons + shift);
        weighted += (coupons + shift) * last;
        price += last;

        return weighted / price / frequency;
    }

    private DateTime Date(ArgumentReader r, int index) => dates.FromSerial(r.Number(index));

    private (DateTime Settlement, DateTime Maturity) Period(ArgumentReader r) =>
        (Date(r, 0), Date(r, 1));

    private static double? Span(DateTime start, DateTime end, int basis) =>
        start < end ? DayCount.YearFraction(start, end, basis) : null;

    private static CellValue Failure(ArgumentReader r) => r.HasError ? r.Error : F.OutOfDomain();
}
