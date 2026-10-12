namespace DPOper.Models;

/// <summary>Un BTP a tasso fisso con cedola semestrale: i dati anagrafici e l'ultimo prezzo noto (corso secco per 100).</summary>
public sealed record BtpBond(string Isin, string Description, decimal Coupon, DateOnly Maturity, decimal Price);
