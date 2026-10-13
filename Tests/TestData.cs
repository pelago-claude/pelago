using Pelago.Models;

namespace Pelago.Tests;

/// <summary>Costruttori rapidi di operazioni per i test (stesse convenzioni dell'export: acquisti con importo negativo, ecc.).</summary>
internal static class Ops
{
    public static DateTime At(string date, string time = "10:00:00") =>
        DateTime.Parse($"{date}T{time}", System.Globalization.CultureInfo.InvariantCulture);

    public static Operation Transfer(string date, decimal amount) => new()
    {
        DateTime = At(date, "08:00:00"), Date = DateOnly.Parse(date), Category = "CASH",
        Type = amount >= 0 ? "TRANSFER_INSTANT_INBOUND" : "TRANSFER_INSTANT_OUTBOUND", Amount = amount,
    };

    public static Operation Buy(string date, string key, string cls, decimal shares, decimal price, decimal fee = 0m, string name = "", string description = "") => new()
    {
        DateTime = At(date), Date = DateOnly.Parse(date), Category = "TRADING", Type = "BUY", AssetClass = cls,
        Name = string.IsNullOrEmpty(name) ? key : name, Symbol = key, Shares = shares, Price = price,
        Amount = -shares * price, Fee = fee == 0m ? null : -fee, Description = description,
    };

    public static Operation Sell(string date, string key, string cls, decimal shares, decimal price, decimal fee = 0m, decimal tax = 0m, string name = "") => new()
    {
        DateTime = At(date, "11:00:00"), Date = DateOnly.Parse(date), Category = "TRADING", Type = "SELL", AssetClass = cls,
        Name = string.IsNullOrEmpty(name) ? key : name, Symbol = key, Shares = -shares, Price = price,
        Amount = shares * price, Fee = fee == 0m ? null : -fee, Tax = tax == 0m ? null : -tax,
    };

    public static Operation Interest(string date, decimal gross, decimal tax) => new()
    {
        DateTime = At(date, "07:00:00"), Date = DateOnly.Parse(date), Category = "CASH", Type = "INTEREST_PAYMENT",
        Amount = gross, Tax = -tax,
    };
}
