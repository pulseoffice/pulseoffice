using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class FinancialFunctions(IRangeMaterializer ranges, IDateSystem dates) : IFunctionProvider
{
    public string Category => "Finanza";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Raw("PV", 3, 5, r =>
        {
            var rate = r.Number(0);
            var periods = r.Number(1);
            var payment = r.Number(2);
            var future = r.Number(3, 0);
            var dueAtStart = r.Boolean(4, false);
            return r.HasError ? r.Error
                : F.Number(FinancialMath.PresentValue(rate, periods, payment, future, dueAtStart));
        })

        .Raw("FV", 3, 5, r =>
        {
            var rate = r.Number(0);
            var periods = r.Number(1);
            var payment = r.Number(2);
            var present = r.Number(3, 0);
            var dueAtStart = r.Boolean(4, false);
            return r.HasError ? r.Error
                : F.Number(FinancialMath.FutureValue(rate, periods, payment, present, dueAtStart));
        })

        .Raw("PMT", 3, 5, r =>
        {
            var rate = r.Number(0);
            var periods = r.Number(1);
            var present = r.Number(2);
            var future = r.Number(3, 0);
            var dueAtStart = r.Boolean(4, false);
            return r.HasError ? r.Error
                : F.Number(FinancialMath.Payment(rate, periods, present, future, dueAtStart));
        })

        .Raw("NPER", 3, 5, r =>
        {
            var rate = r.Number(0);
            var payment = r.Number(1);
            var present = r.Number(2);
            var future = r.Number(3, 0);
            var dueAtStart = r.Boolean(4, false);
            if (r.HasError) return r.Error;

            return FinancialMath.Periods(rate, payment, present, future, dueAtStart) is { } periods
                ? F.Number(periods) : F.OutOfDomain();
        })

        .Raw("RATE", 3, 6, r =>
        {
            var periods = r.Number(0);
            var payment = r.Number(1);
            var present = r.Number(2);
            var future = r.Number(3, 0);
            var dueAtStart = r.Boolean(4, false);
            var guess = r.Number(5, 0.1);
            if (r.HasError) return r.Error;

            return FinancialMath.Rate(periods, payment, present, future, dueAtStart, guess) is { } rate
                ? F.Number(rate) : F.OutOfDomain();
        })

        .Raw("IPMT", 4, 6, r => Installment(r, interestPart: true))
        .Raw("PPMT", 4, 6, r => Installment(r, interestPart: false))

        .Raw("ISPMT", 4, 4, r =>
        {
            var rate = r.Number(0);
            var period = r.Number(1);
            var periods = r.Number(2);
            var present = r.Number(3);
            if (r.HasError) return r.Error;

            return F.Number(present * rate * (period / periods - 1));
        })

        .Raw("CUMIPMT", 6, 6, r => Cumulative(r, interestPart: true))
        .Raw("CUMPRINC", 6, 6, r => Cumulative(r, interestPart: false))

        .Raw("NPV", 2, int.MaxValue, r =>
        {
            var rate = r.Number(0);
            if (r.HasError) return r.Error;

            var flows = new List<double>();
            for (var i = 1; i < r.Count; i++)
                foreach (var value in r.Values(i))
                {
                    if (value.IsError) return value;
                    if (value.Kind == ValueKind.Number) flows.Add(value.AsNumber());
                }

            return F.Number(FinancialMath.NetPresentValue(rate, flows));
        })

        .Raw("IRR", 1, 2, r =>
        {
            if (r.Raw(0) is ReferenceListValue) return CellValue.FromError(CellError.Value);
            var flows = Numbers(r, 0);
            var guess = r.Number(1, 0.1);
            if (r.HasError) return r.Error;

            return FinancialMath.InternalRateOfReturn(flows, guess) is { } rate
                ? F.Number(rate) : F.OutOfDomain();
        })

        .Raw("MIRR", 3, 3, r =>
        {
            var flows = Numbers(r, 0);
            var financeRate = r.Number(1);
            var reinvestRate = r.Number(2);
            if (r.HasError) return r.Error;
            if (flows.Count < 2) return F.OutOfDomain();

            var periods = flows.Count - 1;
            var negative = 0.0;
            var positive = 0.0;

            for (var i = 0; i < flows.Count; i++)
            {
                if (flows[i] < 0) negative += flows[i] / M.Pow(1 + financeRate, i);
                else positive += flows[i] * M.Pow(1 + reinvestRate, periods - i);
            }

            if (negative == 0) return F.DivideByZero();
            return F.Number(M.Pow(-positive / negative, 1.0 / periods) - 1);
        })

        .Raw("XNPV", 3, 3, r =>
        {
            var rate = r.Number(0);
            var flows = Numbers(r, 1);
            var schedule = Numbers(r, 2);
            if (r.HasError) return r.Error;
            if (flows.Count != schedule.Count || flows.Count == 0) return F.OutOfDomain();

            var start = dates.FromSerial(schedule[0]);
            var total = 0.0;
            for (var i = 0; i < flows.Count; i++)
            {
                var days = (dates.FromSerial(schedule[i]) - start).TotalDays;
                total += flows[i] / M.Pow(1 + rate, days / 365.0);
            }
            return F.Number(total);
        })

        .Raw("XIRR", 2, 3, r =>
        {
            var flows = Numbers(r, 0);
            var schedule = Numbers(r, 1);
            var guess = r.Number(2, 0.1);
            if (r.HasError) return r.Error;
            if (flows.Count != schedule.Count || flows.Count < 2) return F.OutOfDomain();
            if (schedule.Any(serial => M.Floor(serial) < M.Floor(schedule[0])) || !flows.Any(flow => flow > 0) || !flows.Any(flow => flow < 0))
                return F.OutOfDomain();

            var start = dates.FromSerial(schedule[0]);
            var offsets = schedule.Select(s => (dates.FromSerial(s) - start).TotalDays / 365.0).ToList();

            double Npv(double rate)
            {
                var total = 0.0;
                for (var i = 0; i < flows.Count; i++) total += flows[i] / M.Pow(1 + rate, offsets[i]);
                return total;
            }

            return FinancialMath.SolveRate(Npv, guess) is { } result ? F.Number(result) : F.OutOfDomain();
        })

        .Raw("SLN", 3, 3, r =>
        {
            var cost = r.Number(0);
            var salvage = r.Number(1);
            var life = r.Number(2);
            if (r.HasError) return r.Error;
            return life == 0 ? F.DivideByZero()
                : F.Number(FinancialMath.StraightLineDepreciation(cost, salvage, life));
        })

        .Raw("SYD", 4, 4, r =>
        {
            var cost = r.Number(0);
            var salvage = r.Number(1);
            var life = r.Number(2);
            var period = r.Number(3);
            if (r.HasError) return r.Error;
            if (life <= 0 || period < 1 || period > life) return F.OutOfDomain();
            return F.Number(FinancialMath.SumOfYearsDigits(cost, salvage, life, period));
        })

        .Raw("DDB", 4, 5, r =>
        {
            var cost = r.Number(0);
            var salvage = r.Number(1);
            var life = r.Number(2);
            var period = r.Number(3);
            var factor = r.Number(4, 2);
            if (r.HasError) return r.Error;
            if (cost < 0 || salvage < 0 || salvage > cost || life <= 0 || period < 1 || period > life || factor <= 0) return F.OutOfDomain();
            return F.Number(FinancialMath.DoubleDecliningPeriod(cost, salvage, life, period, factor));
        })

        .Raw("EFFECT", 2, 2, r =>
        {
            var nominal = r.Number(0);
            var periods = M.Floor(r.Number(1));
            if (r.HasError) return r.Error;
            if (nominal <= 0 || periods < 1) return F.OutOfDomain();
            return F.Number(M.Pow(1 + nominal / periods, periods) - 1);
        })

        .Raw("NOMINAL", 2, 2, r =>
        {
            var effective = r.Number(0);
            var periods = M.Floor(r.Number(1));
            if (r.HasError) return r.Error;
            if (effective <= 0 || periods < 1) return F.OutOfDomain();
            return F.Number((M.Pow(effective + 1, 1 / periods) - 1) * periods);
        })

        .Numeric2("DOLLARDE", (fractional, denominator) =>
        {
            denominator = M.Truncate(denominator);
            if (denominator < 1) return F.OutOfDomain();
            var whole = M.Truncate(fractional);
            var decimals = (fractional - whole) * M.Pow(10, M.Ceiling(M.Log10(denominator)));
            return F.Number(whole + decimals / denominator);
        })

        .Numeric2("DOLLARFR", (value, denominator) =>
        {
            denominator = M.Truncate(denominator);
            if (denominator < 1) return F.OutOfDomain();
            var whole = M.Truncate(value);
            var fraction = (value - whole) * denominator;
            return F.Number(whole + fraction / M.Pow(10, M.Ceiling(M.Log10(denominator))));
        })

        .Raw("PDURATION", 3, 3, r =>
        {
            var rate = r.Number(0);
            var present = r.Number(1);
            var future = r.Number(2);
            if (r.HasError) return r.Error;
            if (rate <= 0 || present <= 0 || future <= 0) return F.OutOfDomain();
            return F.Number((M.Log(future) - M.Log(present)) / M.Log(1 + rate));
        })

        .Raw("RRI", 3, 3, r =>
        {
            var periods = r.Number(0);
            var present = r.Number(1);
            var future = r.Number(2);
            if (r.HasError) return r.Error;
            if (periods <= 0 || present <= 0) return F.OutOfDomain();
            return F.Number(M.Pow(future / present, 1 / periods) - 1);
        })

        .Raw("FVSCHEDULE", 2, 2, r =>
        {
            var principal = r.Number(0);
            var rates = Numbers(r, 1);
            if (r.HasError) return r.Error;

            var value = principal;
            foreach (var rate in rates) value *= 1 + rate;
            return F.Number(value);
        })

        .Build();

    private List<double> Numbers(ArgumentReader r, int index)
    {
        var grid = ranges.Materialize(r.Raw(index), r.Context);
        var values = new List<double>();

        foreach (var value in grid.InReadingOrder())
        {
            if (value.IsError) { r.Fail(value.Error); return values; }
            if (value.Kind == ValueKind.Number) values.Add(value.AsNumber());
        }
        return values;
    }

    private static CellValue Installment(ArgumentReader r, bool interestPart)
    {
        var rate = r.Number(0);
        var period = r.Number(1);
        var periods = r.Number(2);
        var present = r.Number(3);
        var future = r.Number(4, 0);
        var dueAtStart = r.Boolean(5, false);
        if (r.HasError) return r.Error;
        if (period < 1 || period > periods) return F.OutOfDomain();

        var payment = FinancialMath.Payment(rate, periods, present, future, dueAtStart);
        var interest = InterestOf(rate, period, payment, present, dueAtStart);
        return F.Number(interestPart ? interest : payment - interest);
    }

    private static CellValue Cumulative(ArgumentReader r, bool interestPart)
    {
        var rate = r.Number(0);
        var periods = r.Number(1);
        var present = r.Number(2);
        var start = (int)r.Number(3);
        var end = (int)r.Number(4);
        var type = r.Number(5);
        var dueAtStart = type != 0;
        if (r.HasError) return r.Error;
        if (rate <= 0 || periods <= 0 || present <= 0 || type is not (0 or 1) || start < 1 || end < start || end > periods)
            return F.OutOfDomain();

        var payment = FinancialMath.Payment(rate, periods, present, 0, dueAtStart);
        var principal = dueAtStart
            ? (start == 1 ? -present : FinancialMath.FutureValue(rate, start - 2, payment, present, true) - payment)
              - FinancialMath.FutureValue(rate, end - 1, payment, present, true) + payment
            : FinancialMath.FutureValue(rate, start - 1, payment, present, false)
              - FinancialMath.FutureValue(rate, end, payment, present, false);
        return F.Number(interestPart ? payment * (end - start + 1.0) - principal : principal);
    }

    private static double InterestOf(double rate, double period, double payment, double present, bool dueAtStart)
    {
        if (period == 1) return dueAtStart ? 0 : -present * rate;
        return dueAtStart
            ? (FinancialMath.FutureValue(rate, period - 2, payment, present, true) - payment) * rate
            : FinancialMath.FutureValue(rate, period - 1, payment, present, false) * rate;
    }
}
