using Pelago.Models;

namespace Pelago.Services;

/// <summary>Un incasso futuro di un'obbligazione posseduta: una cedola o il rimborso a scadenza.</summary>
public sealed record CouponEvent(DateOnly Date, string Isin, string Name, string Kind, decimal Gross, decimal Net);

public sealed record CouponPlan(List<CouponEvent> Events, List<string> Unknown);

/// <summary>
/// Calendario degli incassi dei BTP che possiedi: le cedole semestrali ancora da incassare (al lordo e al netto
/// dell'imposta) e il rimborso a scadenza. Servono i dati del BTP (cedola e scadenza) dall'elenco scaricato da Borsa Italiana:
/// per un'obbligazione che non è nell'elenco non si può costruire il calendario.
/// </summary>
public static class CouponSchedule
{
    public static CouponPlan Build(
        IEnumerable<(string Isin, string Name, decimal Nominal)> held,
        IReadOnlyList<BtpBond> catalog,
        decimal taxRate,
        DateOnly today)
    {
        var events = new List<CouponEvent>();
        var unknown = new List<string>();

        foreach (var (isin, name, nominal) in held)
        {
            var bond = catalog.FirstOrDefault(b => b.Isin == isin);
            if (bond is null)
            {
                unknown.Add(name);
                continue;
            }

            if (bond.Maturity <= today)
            {
                continue;
            }

            var coupon = nominal * bond.Coupon / 2m;
            // Le date si contano a ritroso dalla scadenza (sempre dalla scadenza, non dalla data precedente, così
            // un fine mese non fa slittare le cedole successive).
            for (var k = 0; ; k++)
            {
                var date = bond.Maturity.AddMonths(-6 * k);
                if (date <= today)
                {
                    break;
                }

                events.Add(new CouponEvent(date, isin, name, "Cedola", coupon, coupon * (1m - taxRate)));
            }

            events.Add(new CouponEvent(bond.Maturity, isin, name, "Rimborso", nominal, nominal));
        }

        return new CouponPlan(events.OrderBy(e => e.Date).ThenBy(e => e.Kind).ToList(), unknown);
    }

    /// <summary>Totali per mese (lordo e netto), per il grafico.</summary>
    public static List<(string Month, decimal Gross, decimal Net)> ByMonth(IEnumerable<CouponEvent> events) => events
        .GroupBy(e => $"{e.Date.Year:0000}-{e.Date.Month:00}")
        .OrderBy(g => g.Key)
        .Select(g => (g.Key, g.Sum(e => e.Gross), g.Sum(e => e.Net)))
        .ToList();
}
