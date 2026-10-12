namespace DPOper.Services;

/// <summary>Prepara i dati (già in forma semplice: nomi, colori, numeri) che <c>wwwroot/js/charts.js</c> disegna con ECharts.</summary>
public static class ChartData
{
    private const string DateFormat = "dd/MM/yy";

    private static readonly (string Key, string Label, string Color)[] CompositionStacks =
        AssetClassPalette.Categories
            .Where(c => c.Key != "INTEREST")
            .Select(c => (c.Key, c.Label, Fmt.Hex(c.Color)))
            .ToArray();

    /// <summary>
    /// La mappa del portafoglio: un riquadro per titolo, grande quanto il suo valore e colorato dal guadagno o dalla perdita sul costo (vedi <see cref="HeatColor"/>),
    /// raggruppato per categoria. I nomi arrivano già pronti (<paramref name="nameOf"/>) perché dipendono dai nomi scelti dall'utente e da quelli degli emittenti.
    /// </summary>
    public static object HeatMapTree(HeatMap map, Func<HeatItem, string> nameOf) => new
    {
        labels = new
        {
            value = Loc.T("Valore"),
            weight = Loc.T("Peso sul conto"),
            shares = Loc.T("Quantità"),
            price = Loc.T("Prezzo"),
            cost = Loc.T("Costo"),
            pnl = Loc.T("Plus/minus latente"),
            noCost = Loc.T("costo non noto"),
        },
        groups = AssetClassPalette.Categories
            .Select(c => (c.Key, c.Label, Items: map.Items.Where(i => i.AssetClass == c.Key).ToList()))
            .Where(g => g.Items.Count > 0)
            .Select(g => new
            {
                name = Loc.T(g.Label),
                value = g.Items.Sum(i => i.Value),
                items = g.Items.Select(i => new
                {
                    key = i.Key,
                    name = nameOf(i),
                    value = i.Value,
                    color = HeatColor.For(i.PnlPct),
                    pct = i.PnlPct,
                    pnl = i.Pnl,
                    cost = i.Cost,
                    shares = i.Shares,
                    price = i.Price,
                    weight = i.Weight,
                    category = Loc.T(g.Label),
                }).ToArray(),
            }).ToArray(),
    };

    public static object Donut(DailySnapshot s) => new
    {
        centerLabel = Loc.T("Totale conto"),
        centerValue = Fmt.Money(s.Total),
        items = new[]
        {
            new { name = Loc.T("Disponibilità"), value = s.Cash, color = "#4682B4" },
            new { name = Loc.T("Titoli"), value = s.Securities, color = "#FF8C00" },
            new { name = Loc.T("Obbligazioni"), value = s.Bonds, color = "#3CB371" },
            new { name = Loc.T("Crypto"), value = s.Crypto, color = "#9370DB" },
        },
    };

    /// <summary>Linee giornaliere: disponibilità, titoli, obbligazioni, crypto e totale.</summary>
    public static object Trend(IReadOnlyList<DailySnapshot> snapshots, decimal? goal = null) => new
    {
        dates = snapshots.Select(s => s.Date.ToString(DateFormat)).ToArray(),
        goal,
        series = new object[]
        {
            new { name = Loc.T("Disponibilità"), color = "#4682B4", values = snapshots.Select(s => s.Cash).ToArray() },
            new { name = Loc.T("Titoli"), color = "#FF8C00", values = snapshots.Select(s => s.Securities).ToArray() },
            new { name = Loc.T("Obbligazioni"), color = "#3CB371", values = snapshots.Select(s => s.Bonds).ToArray() },
            new { name = Loc.T("Crypto"), color = "#9370DB", values = snapshots.Select(s => s.Crypto).ToArray() },
            new { name = Loc.T("Totale"), color = "text", width = 3, values = snapshots.Select(s => s.Total).ToArray() },
        },
    };

    /// <summary>Totale contro versamenti netti (grafico della panoramica).</summary>
    public static object TotalVsContributions(IReadOnlyList<DailySnapshot> snapshots, decimal? goal = null) => new
    {
        dates = snapshots.Select(s => s.Date.ToString(DateFormat)).ToArray(),
        goal,
        series = new object[]
        {
            new { name = Loc.T("Totale"), color = "#3b6ef5", width = 3, area = true, values = snapshots.Select(s => s.Total).ToArray() },
            new { name = Loc.T("Versamenti netti"), color = "text", dashed = true, values = snapshots.Select(s => s.Contributions).ToArray() },
        },
    };

    public static object Composition(IReadOnlyList<PortfolioAsOf> points) => new
    {
        labels = points.Select(p => p.Date.ToString(DateFormat)).ToArray(),
        stacks = CompositionStacks.Select(c => new
        {
            name = Loc.T(c.Label),
            color = c.Color,
            values = points.Select(p => c.Key == "CASH"
                ? p.Cash
                : p.Assets.Where(a => a.AssetClass == c.Key).Sum(a => a.Value)).ToArray(),
        }).ToArray(),
        totals = points.Select(p => p.Total).ToArray(),
        contributions = points.Select(p => p.Contributions).ToArray(),
        gaps = points.Select(p => p.Gap).ToArray(),
    };

    /// <summary>Colonne mensili: base fino ai versamenti, cappello verde/rosso con lo scostamento.</summary>
    public static object Gap(IReadOnlyList<DailySnapshot> monthly) => new
    {
        labels = monthly.Select(s => s.Date.ToString("MM/yyyy")).ToArray(),
        totals = monthly.Select(s => s.Total).ToArray(),
        contributions = monthly.Select(s => s.Contributions).ToArray(),
        gaps = monthly.Select(s => s.Gap).ToArray(),
        @base = monthly.Select(s => Math.Min(s.Total, Math.Max(0m, s.Contributions))).ToArray(),
        capPos = monthly.Select(s => Math.Max(0m, s.Gap)).ToArray(),
        capNeg = monthly.Select(s => s.Gap < 0m ? -s.Gap : 0m).ToArray(),
    };

    public static object QuoteLine(string name, IEnumerable<(DateTime Timestamp, decimal Price)> quotes)
    {
        var ordered = quotes.OrderBy(q => q.Timestamp).ToList();
        return new
        {
            scale = true,
            dates = ordered.Select(q => q.Timestamp.ToLocalTime().ToString("dd/MM/yy HH:mm")).ToArray(),
            series = new object[] { new { name, color = "#3b6ef5", width = 2, area = true, values = ordered.Select(q => q.Price).ToArray() } },
        };
    }

    /// <summary>Andamento di un cambio (o di un rendimento): numeri semplici, senza simbolo di valuta.</summary>
    public static object RateLine(string name, IEnumerable<(DateOnly Date, decimal Value)> points, int decimals = 4)
    {
        var list = points.OrderBy(p => p.Date).ToList();
        return new
        {
            scale = true,
            plain = true,
            decimals,
            dates = list.Select(p => p.Date.ToString(DateFormat)).ToArray(),
            series = new object[] { new { name, color = "#3b6ef5", width = 2, area = true, values = list.Select(p => p.Value).ToArray() } },
        };
    }

    /// <summary>Una o più serie di rendimenti (in %) sullo stesso asse dei tempi: le date mancanti in una serie restano vuote.</summary>
    public static object YieldLines(bool monthly, string suffix, int decimals, params (string Name, string Color, bool Dashed, IReadOnlyList<YieldPoint> Points)[] series)
    {
        var dates = series.SelectMany(s => s.Points.Select(p => p.Date)).Distinct().OrderBy(d => d).ToList();
        var format = monthly ? "MM/yyyy" : "dd/MM/yy";
        return new
        {
            scale = true,
            plain = true,
            decimals,
            suffix,
            dates = dates.Select(d => d.ToString(format, System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            series = series.Select(s =>
            {
                var byDate = s.Points.ToDictionary(p => p.Date, p => p.Value);
                return new
                {
                    name = s.Name,
                    color = s.Color,
                    width = s.Dashed ? 2 : 3,
                    dashed = s.Dashed,
                    area = !s.Dashed && series.Length == 1,
                    values = dates.Select(d => byDate.TryGetValue(d, out var v) ? (decimal?)v : null).ToArray(),
                };
            }).ToArray(),
        };
    }

    /// <summary>Curve dei rendimenti per scadenza: un'etichetta per scadenza e una serie per data (oggi, un mese fa, un anno fa).</summary>
    public static object Curves(IEnumerable<string> labels, params (string Name, string Color, bool Dashed, decimal?[] Values)[] series) => new
    {
        scale = true,
        plain = true,
        decimals = 2,
        suffix = "%",
        symbols = true,
        dates = labels.ToArray(),
        series = series.Select(s => new { name = s.Name, color = s.Color, width = s.Dashed ? 2 : 3, dashed = s.Dashed, values = s.Values }).ToArray(),
    };

    public static List<DailySnapshot> LastOfEachMonth(IEnumerable<DailySnapshot> snapshots) => snapshots
        .GroupBy(s => new { s.Date.Year, s.Date.Month })
        .Select(g => g.OrderBy(s => s.Date).Last())
        .OrderBy(s => s.Date)
        .ToList();

    /// <summary>Rendimento cumulato (TWR) giorno per giorno, in percentuale.</summary>
    public static object CumulativeTwr(IReadOnlyList<(DateOnly Date, double Cumulative)> points) => new
    {
        percent = true,
        dates = points.Select(p => p.Date.ToString(DateFormat)).ToArray(),
        series = new object[]
        {
            new { name = Loc.T("Rendimento cumulato (TWR)"), color = "#3b6ef5", width = 3, area = true, values = points.Select(p => p.Cumulative).ToArray() },
        },
    };

    /// <summary>Risultato realizzato accumulato giorno per giorno, in euro.</summary>
    public static object CumulativeResult(IReadOnlyList<(DateOnly Date, decimal Cumulative)> points) => new
    {
        dates = points.Select(p => p.Date.ToString(DateFormat)).ToArray(),
        series = new object[]
        {
            new { name = Loc.T("Risultato cumulato"), color = "#3b6ef5", width = 3, area = true, values = points.Select(p => p.Cumulative).ToArray() },
        },
    };

    /// <summary>Colonne (semplici o impilate) con un'etichetta per colonna e una o più serie colorate.</summary>
    public static object Bars(IEnumerable<string> labels, bool stacked, params (string Name, string Color, decimal[] Values)[] series) => new
    {
        labels = labels.ToArray(),
        stacked,
        series = series.Select(s => new { name = s.Name, color = s.Color, values = s.Values }).ToArray(),
    };
}
