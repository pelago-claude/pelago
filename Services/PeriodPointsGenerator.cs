namespace DPOper.Services;

public enum PeriodType
{
    Daily,
    Weekly,
    Monthly,
    Bimonthly,
    Quarterly,
    FourMonthly,
    SemiAnnual,
    Annual,
    Ytd,
}

/// <summary>
/// Genera le date da mostrare in un grafico "a passo", ancorate a oggi e risalendo indietro fino
/// alla prima data disponibile. Per i passi mensili (mensile, bimestrale, ...) il giorno del mese è
/// sempre quello di oggi, non quello (eventualmente troncato) del punto precedente: se un mese non
/// ha quel giorno (es. oggi è il 31 e il mese non ha 31 giorni) si usa l'ultimo giorno di quel mese.
/// YTD è un caso a parte: un solo intervallo dall'1 gennaio di quest'anno ad oggi, non una serie a
/// ritroso.
/// </summary>
public static class PeriodPointsGenerator
{
    public static readonly (PeriodType Type, string Label)[] Options =
    {
        (PeriodType.Daily, "Giornaliero"),
        (PeriodType.Weekly, "Settimanale"),
        (PeriodType.Monthly, "Mensile"),
        (PeriodType.Bimonthly, "Bimestrale"),
        (PeriodType.Quarterly, "Trimestrale"),
        (PeriodType.FourMonthly, "Quadrimestrale"),
        (PeriodType.SemiAnnual, "Semestrale"),
        (PeriodType.Annual, "Annuale"),
        (PeriodType.Ytd, "YTD"),
    };

    public static List<DateOnly> Generate(DateOnly today, DateOnly firstDate, PeriodType period)
    {
        if (firstDate > today)
        {
            return new List<DateOnly> { today };
        }

        if (period == PeriodType.Ytd)
        {
            var yearStart = new DateOnly(today.Year, 1, 1);
            if (yearStart < firstDate)
            {
                yearStart = firstDate;
            }
            return yearStart == today ? new List<DateOnly> { today } : new List<DateOnly> { yearStart, today };
        }

        var points = new List<DateOnly>();
        var stepDays = period switch
        {
            PeriodType.Daily => 1,
            PeriodType.Weekly => 7,
            _ => 0, // i passi mensili non usano un numero fisso di giorni: vedi AddMonthsClamped
        };
        var monthStep = period switch
        {
            PeriodType.Monthly => 1,
            PeriodType.Bimonthly => 2,
            PeriodType.Quarterly => 3,
            PeriodType.FourMonthly => 4,
            PeriodType.SemiAnnual => 6,
            PeriodType.Annual => 12,
            _ => 0,
        };

        var current = today;
        var step = 0;
        while (current >= firstDate)
        {
            points.Add(current);
            step++;
            current = monthStep > 0
                ? AddMonthsClamped(today, monthStep * step)
                : today.AddDays(-stepDays * step);
        }

        points.Reverse();
        return points;
    }

    private static DateOnly AddMonthsClamped(DateOnly anchor, int monthsBack)
    {
        var totalMonths = anchor.Year * 12 + (anchor.Month - 1) - monthsBack;
        var targetYear = totalMonths / 12;
        var targetMonth = totalMonths % 12 + 1;
        if (targetMonth <= 0)
        {
            targetMonth += 12;
            targetYear--;
        }

        var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var day = Math.Min(anchor.Day, daysInMonth);
        return new DateOnly(targetYear, targetMonth, day);
    }
}
