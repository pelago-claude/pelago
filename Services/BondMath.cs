namespace Pelago.Services;

/// <summary>Posizione di una data di regolamento nel calendario cedolare di un titolo.</summary>
public sealed record CouponInfo(
    DateOnly Previous,
    DateOnly Next,
    int DaysFromPrevious,
    int DaysInPeriod,
    int DaysToNext,
    int Remaining,
    int DaysToMaturity);

/// <summary>
/// Replica delle funzioni finanziarie di Excel usate dal calcolatore BTP (COUPDAYBS, COUPDAYS, COUPNCD,
/// COUPNUM, YEARFRAC base 1, YIELD/RENDIMENTO con cedola semestrale e base effettiva/effettiva).
/// </summary>
public static class BondMath
{
    private const double Frequency = 2;

    /// <summary>Equivalente di WORKDAY(data; giorni) senza festività (come nel foglio Excel).</summary>
    public static DateOnly AddWorkdays(DateOnly start, int days)
    {
        var date = start;
        var added = 0;
        while (added < days)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                added++;
            }
        }
        return date;
    }

    /// <summary>
    /// Data della cedola "stepsBack" semestri prima della scadenza. Se la scadenza è l'ultimo giorno del
    /// mese lo sono anche le cedole (regola di Excel); altrimenti si tronca all'ultimo giorno disponibile.
    /// </summary>
    private static DateOnly CouponDate(DateOnly maturity, int stepsBack)
    {
        var totalMonths = maturity.Year * 12 + (maturity.Month - 1) - 6 * stepsBack;
        var year = totalMonths / 12;
        var month = totalMonths % 12 + 1;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var maturityIsEndOfMonth = maturity.Day == DateTime.DaysInMonth(maturity.Year, maturity.Month);
        var day = maturityIsEndOfMonth ? daysInMonth : Math.Min(maturity.Day, daysInMonth);
        return new DateOnly(year, month, day);
    }

    public static CouponInfo? GetCouponInfo(DateOnly settlement, DateOnly maturity)
    {
        if (settlement >= maturity)
        {
            return null;
        }

        var next = maturity;
        for (var k = 0; ; k++)
        {
            var previous = CouponDate(maturity, k + 1);
            if (previous <= settlement)
            {
                return new CouponInfo(
                    previous,
                    next,
                    settlement.DayNumber - previous.DayNumber,
                    next.DayNumber - previous.DayNumber,
                    next.DayNumber - settlement.DayNumber,
                    k + 1,
                    maturity.DayNumber - settlement.DayNumber);
            }
            next = previous;
        }
    }

    /// <summary>YEARFRAC con base 1 (effettivo/effettivo), come Excel.</summary>
    public static double YearFrac(DateOnly start, DateOnly end)
    {
        if (start == end)
        {
            return 0;
        }
        if (start > end)
        {
            (start, end) = (end, start);
        }

        var days = end.DayNumber - start.DayNumber;
        double denominator;

        var withinOneYear = end.Year == start.Year
            || (end.Year == start.Year + 1 && (start.Month > end.Month || (start.Month == end.Month && start.Day >= end.Day)));

        if (withinOneYear)
        {
            if (end.Year == start.Year)
            {
                denominator = DateTime.IsLeapYear(start.Year) ? 366 : 365;
            }
            else
            {
                var includesLeapDay =
                    (DateTime.IsLeapYear(start.Year) && start <= new DateOnly(start.Year, 2, 29))
                    || (DateTime.IsLeapYear(end.Year) && end >= new DateOnly(end.Year, 2, 29));
                denominator = includesLeapDay ? 366 : 365;
            }
        }
        else
        {
            var totalDays = 0;
            for (var year = start.Year; year <= end.Year; year++)
            {
                totalDays += DateTime.IsLeapYear(year) ? 366 : 365;
            }
            denominator = totalDays / (double)(end.Year - start.Year + 1);
        }

        return days / denominator;
    }

    /// <summary>Prezzo secco (per 100) dato il rendimento annuo <paramref name="yield"/>: formula PRICE di Excel.</summary>
    private static double PriceFromYield(double yield, double rate, double redemption, CouponInfo info)
    {
        var coupon = 100 * rate / Frequency;
        var a = (double)info.DaysFromPrevious;
        var e = (double)info.DaysInPeriod;
        var dsc = (double)info.DaysToNext;
        var n = info.Remaining;

        if (n == 1)
        {
            return (redemption + coupon) / (1 + dsc / e * yield / Frequency) - a / e * coupon;
        }

        var growth = 1 + yield / Frequency;
        var price = redemption / Math.Pow(growth, n - 1 + dsc / e);
        for (var k = 1; k <= n; k++)
        {
            price += coupon / Math.Pow(growth, k - 1 + dsc / e);
        }
        return price - a / e * coupon;
    }

    /// <summary>
    /// Rendimento annuo a scadenza (RENDIMENTO di Excel, cedola semestrale, base 1) dato il corso secco
    /// <paramref name="price"/> per 100. <paramref name="redemption"/> è il rimborso per 100 (può essere
    /// ridotto dall'imposta per il calcolo netto). NaN se il titolo è scaduto o il calcolo non converge.
    /// </summary>
    public static double Yield(DateOnly settlement, DateOnly maturity, double rate, double price, double redemption)
    {
        var info = GetCouponInfo(settlement, maturity);
        if (info is null)
        {
            return double.NaN;
        }

        var coupon = 100 * rate / Frequency;
        var a = (double)info.DaysFromPrevious;
        var e = (double)info.DaysInPeriod;

        if (info.Remaining == 1)
        {
            return ((redemption + coupon) / (price + a / e * coupon) - 1) * Frequency * e / info.DaysToNext;
        }

        // Il prezzo è decrescente nel rendimento: bisezione.
        var low = -0.9;
        var high = 5.0;
        if (PriceFromYield(low, rate, redemption, info) < price || PriceFromYield(high, rate, redemption, info) > price)
        {
            return double.NaN;
        }

        for (var i = 0; i < 200; i++)
        {
            var mid = (low + high) / 2;
            if (PriceFromYield(mid, rate, redemption, info) > price)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        return (low + high) / 2;
    }
}
