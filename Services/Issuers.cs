namespace Pelago.Services;

/// <summary>Gli emittenti dei derivati che si trovano nel portafoglio (Trade Republic offre solo prodotti di questi due).</summary>
public enum Issuer
{
    Vontobel,
    SocieteGenerale,
}

/// <summary>
/// Chi ha emesso un derivato. Lo si capisce dal prefisso del codice ISIN, quindi senza andare in rete e anche per i prodotti già chiusi:
/// Vontobel usa <c>DE000V…</c> e <c>DE000BD…</c>, Société Générale (tramite la sua società tedesca di emissione) <c>DE000F…</c> e <c>DE000S…</c>.
/// Il prefisso vale solo per i derivati: tra le azioni ci sono ISIN tedeschi che cominciano allo stesso modo (per esempio <c>DE000SHL1006</c>),
/// per questo <see cref="ShortName"/> vuole anche la categoria.
/// </summary>
public static class Issuers
{
    public static Issuer? OfIsin(string? isin)
    {
        if (isin is not { Length: 12 })
        {
            return null;
        }

        if (isin.StartsWith("DE000V", StringComparison.OrdinalIgnoreCase) || isin.StartsWith("DE000BD", StringComparison.OrdinalIgnoreCase))
        {
            return Issuer.Vontobel;
        }
        if (isin.StartsWith("DE000F", StringComparison.OrdinalIgnoreCase) || isin.StartsWith("DE000S", StringComparison.OrdinalIgnoreCase))
        {
            return Issuer.SocieteGenerale;
        }
        return null;
    }

    /// <summary>Il nome breve dell'emittente, per le colonne delle liste (nomi propri: non si traducono).</summary>
    public static string Name(Issuer issuer) => issuer switch
    {
        Issuer.Vontobel => "Vontobel",
        Issuer.SocieteGenerale => "Société Générale",
        _ => "",
    };

    /// <summary>L'emittente di un titolo per le liste: solo per i derivati, altrimenti vuoto.</summary>
    public static string ShortName(string? isin, string? assetClass) =>
        string.Equals(assetClass, "DERIVATIVE", StringComparison.OrdinalIgnoreCase) && OfIsin(isin) is { } issuer ? Name(issuer) : "";
}
