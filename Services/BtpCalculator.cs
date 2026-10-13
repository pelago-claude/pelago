using Pelago.Models;

namespace Pelago.Services;

/// <summary>Parametri dell'investimento (gli stessi input gialli del foglio Excel).</summary>
public sealed record BtpParams(decimal Capital, decimal Commission, decimal TaxRate, decimal Lot, DateOnly Settlement);

/// <summary>Dati del titolo alla data di regolamento: calendario cedolare, rateo maturato, anni residui.</summary>
public sealed record BtpContext(BtpBond Bond, BtpParams Params, CouponInfo Coupon, decimal AccruedPer100, double YearsRemaining);

/// <summary>Esito dell'acquisto del capitale a un dato prezzo (corso secco per 100).</summary>
public sealed record BtpScenario(
    decimal Price,
    decimal LotCost,
    int Lots,
    decimal Nominal,
    decimal CleanValue,
    decimal AccruedPaid,
    decimal Commission,
    decimal TotalOutlay,
    decimal LeftoverCash,
    decimal SemiannualCoupon,
    decimal AnnualCoupon,
    decimal AnnualCouponNet,
    decimal CouponYield,
    decimal TotalCoupons,
    decimal Surplus,
    decimal SurplusTax,
    decimal SurplusNet,
    decimal SurplusAnnualized,
    decimal GrossGain,
    decimal NetGain,
    double? GrossYield,
    double? NetYield);

/// <summary>
/// Calcolo "quanti BTP posso comprare con questo capitale e cosa ci guadagno", replicando il foglio
/// Calcolatore_BTP.xlsx: lo stesso acquisto viene valutato al prezzo di mercato e come se il titolo
/// quotasse 100, così si vede quanto vale (in cedole e in surplus a scadenza) comprare sotto la pari.
/// </summary>
public static class BtpCalculator
{
    public static BtpContext? CreateContext(BtpBond bond, BtpParams parameters)
    {
        var coupon = BondMath.GetCouponInfo(parameters.Settlement, bond.Maturity);
        if (coupon is null)
        {
            return null;
        }

        var accrued = bond.Coupon * 100m / 2m * coupon.DaysFromPrevious / coupon.DaysInPeriod;
        var years = BondMath.YearFrac(parameters.Settlement, bond.Maturity);
        return new BtpContext(bond, parameters, coupon, accrued, years);
    }

    public static BtpScenario Compute(BtpContext context, decimal price)
    {
        var p = context.Params;
        var bond = context.Bond;

        var lotCost = (price + context.AccruedPer100) / 100m * p.Lot;
        var lots = lotCost > 0m ? Math.Max(0, (int)Math.Floor((p.Capital - p.Commission) / lotCost)) : 0;
        var nominal = lots * p.Lot;
        var cleanValue = nominal * price / 100m;
        var accruedPaid = nominal * context.AccruedPer100 / 100m;
        var commission = lots > 0 ? p.Commission : 0m;
        var outlay = cleanValue + accruedPaid + commission;

        var semiannual = nominal * bond.Coupon / 2m;
        var annual = nominal * bond.Coupon;
        var annualNet = annual * (1m - p.TaxRate);
        var couponYield = cleanValue > 0m ? annual / cleanValue : 0m;
        var totalCoupons = semiannual * context.Coupon.Remaining;

        var surplus = nominal - cleanValue;
        var surplusTax = Math.Max(0m, surplus) * p.TaxRate;
        var surplusNet = surplus - surplusTax;
        var surplusAnnualized = cleanValue > 0m && context.YearsRemaining > 0
            ? surplus / (decimal)context.YearsRemaining / cleanValue
            : 0m;

        var grossGain = totalCoupons + nominal - outlay;
        var netGain = totalCoupons * (1m - p.TaxRate) + nominal - surplusTax - outlay;

        var rate = (double)bond.Coupon;
        var tax = (double)p.TaxRate;
        var grossYield = BondMath.Yield(p.Settlement, bond.Maturity, rate, (double)price, 100);
        var netYield = BondMath.Yield(p.Settlement, bond.Maturity, rate * (1 - tax), (double)price,
            100 - Math.Max(0, 100 - (double)price) * tax);

        return new BtpScenario(
            price, lotCost, lots, nominal, cleanValue, accruedPaid, commission, outlay, p.Capital - outlay,
            semiannual, annual, annualNet, couponYield, totalCoupons, surplus, surplusTax, surplusNet,
            surplusAnnualized, grossGain, netGain,
            double.IsNaN(grossYield) ? null : grossYield,
            double.IsNaN(netYield) ? null : netYield);
    }
}
