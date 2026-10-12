using System.Collections.Concurrent;

namespace DPOper.Services;

/// <summary>Normalizzazione delle valute di quotazione (pence, centesimi) e conversione in euro.</summary>
public static class CurrencyConverter
{
    public static bool IsEur(string? currency) => string.IsNullOrWhiteSpace(currency) || currency.Equals("EUR", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Alcune borse quotano in sottounità: Londra in pence («GBp», «GBX»: 100 pence = 1 sterlina), il Sudafrica in centesimi
    /// di rand («ZAc»), Israele in agorot («ILA»). Le riporta all'unità principale, da convertire poi in euro.
    /// </summary>
    public static (decimal Price, string Currency) NormalizeMinorUnit(decimal price, string currency) => currency switch
    {
        "GBp" or "GBX" => (price / 100m, "GBP"),
        "ZAc" => (price / 100m, "ZAR"),
        "ILA" => (price / 100m, "ILS"),
        _ => (price, currency),
    };
}

/// <summary>Cambi verso l'euro, recuperati una sola volta per valuta durante un aggiornamento.</summary>
public sealed class RateCache
{
    private readonly ExchangeRateProvider _fx;
    private readonly ConcurrentDictionary<string, Lazy<Task<decimal>>> _rates = new();

    public RateCache(ExchangeRateProvider fx) => _fx = fx;

    /// <summary>Quanti euro vale un'unità della valuta indicata (esempio: USD → 0,86).</summary>
    public Task<decimal> ToEurAsync(string currency, CancellationToken cancellationToken) =>
        _rates.GetOrAdd(currency, c => new Lazy<Task<decimal>>(() => _fx.GetRateAsync(c, "EUR", cancellationToken))).Value;
}
