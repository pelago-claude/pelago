using System.Text.Json.Serialization;

namespace Pelago.Models;

/// <summary>
/// Una quotazione puntuale registrata nello storico di un asset. <see cref="Price"/> è sempre in euro:
/// se la fonte l'ha data in un'altra valuta, il prezzo e la valuta originali restano in
/// <see cref="OriginalPrice"/> / <see cref="OriginalCurrency"/>.
/// </summary>
public sealed class QuoteEntry
{
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Source { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? OriginalPrice { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalCurrency { get; set; }
}
