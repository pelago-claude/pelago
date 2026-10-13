namespace Pelago.Models;

/// <summary>
/// Una singola riga del file di export operazioni (movimento di cassa, acquisto/vendita
/// di un asset finanziario, o evento societario come uno staking reward o l'esercizio di un warrant).
/// </summary>
public sealed class Operation
{
    public DateTime DateTime { get; init; }
    public DateOnly Date { get; init; }
    public string AccountType { get; init; } = "";
    public string Category { get; init; } = "";
    public string Type { get; init; } = "";
    public string AssetClass { get; init; } = "";
    public string Name { get; init; } = "";
    public string Symbol { get; init; } = "";
    public decimal? Shares { get; init; }
    public decimal? Price { get; init; }
    public decimal? Amount { get; init; }
    public decimal? Fee { get; init; }
    public decimal? Tax { get; init; }
    public string Currency { get; init; } = "";
    public string Description { get; init; } = "";
    public string TransactionId { get; init; } = "";
    public string CounterpartyName { get; init; } = "";
    public string CounterpartyIban { get; init; } = "";
    public string PaymentReference { get; init; } = "";

    /// <summary>
    /// Chiave usata per raggruppare le posizioni possedute: preferisce il simbolo (es. ISIN/ticker),
    /// ricadendo sul nome quando il simbolo non è presente nell'export.
    /// </summary>
    public string PositionKey => string.IsNullOrWhiteSpace(Symbol) ? Name : Symbol;

    /// <summary>
    /// Impatto netto sulla liquidità disponibile: l'importo lordo dell'operazione al netto di
    /// commissioni e tasse trattenute (entrambe già espresse con segno negativo nell'export).
    /// </summary>
    public decimal NetCashImpact => (Amount ?? 0m) + (Fee ?? 0m) + (Tax ?? 0m);
}
