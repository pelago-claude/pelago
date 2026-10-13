using Pelago.Models;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

[Collection("AppState")]
public class IssuerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pelago-issuers-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    // ---------------------------------------------------------------- chi ha emesso il prodotto

    [Theory]
    [InlineData("DE000VAAA001", Issuer.Vontobel)]
    [InlineData("DE000BDAA002", Issuer.Vontobel)]
    [InlineData("de000vaad005", Issuer.Vontobel)]
    [InlineData("DE000FAAD001", Issuer.SocieteGenerale)]
    [InlineData("DE000FAAA006", Issuer.SocieteGenerale)]
    [InlineData("DE000SAAA007", Issuer.SocieteGenerale)]
    [InlineData("DE000SAAB008", Issuer.SocieteGenerale)]
    public void The_issuer_is_told_by_the_isin_prefix(string isin, Issuer expected) => Assert.Equal(expected, Issuers.OfIsin(isin));

    [Theory]
    [InlineData("IE00TEST0001")]
    [InlineData("US0378331005")]
    [InlineData("DE000A0D9PT0")]
    [InlineData("DE000V")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_codes_have_no_issuer(string? isin) => Assert.Null(Issuers.OfIsin(isin));

    [Fact]
    public void The_issuer_column_is_filled_only_for_derivatives()
    {
        Assert.Equal("Vontobel", Issuers.ShortName("DE000VAAA001", "DERIVATIVE"));
        Assert.Equal("Société Générale", Issuers.ShortName("DE000FAAD001", "derivative"));

        // alcune azioni tedesche hanno un ISIN che comincia come quello dei derivati: lì l'emittente non c'entra
        Assert.Equal("", Issuers.ShortName("DE000SHL1006", "STOCK"));
        Assert.Equal("", Issuers.ShortName("DE000VAAA001", "STOCK"));
        Assert.Equal("", Issuers.ShortName("DE000VAAA001", null));
        Assert.Equal("", Issuers.ShortName(null, "DERIVATIVE"));
    }

    // ---------------------------------------------------------------- il registro dei nomi degli emittenti

    private IssuerNames NewNames() => new(Path.Combine(_dir, "nomi-emittenti.json"));

    [Fact]
    public void An_issuer_name_is_found_whatever_the_case_and_survives_a_restart()
    {
        var names = NewNames();

        Assert.True(names.Set(" de000faad001 ", "  Turbo   Open-End su Oro "));

        Assert.Equal("Turbo Open-End su Oro", names.Get("DE000FAAD001"));
        Assert.Null(names.Get("DE000FD8Z4Y3"));

        var again = NewNames();
        Assert.Equal("Turbo Open-End su Oro", again.Get("DE000FAAD001"));
        Assert.Equal(1, again.Count);
    }

    [Fact]
    public void Setting_the_same_name_again_changes_nothing_and_empty_names_are_ignored()
    {
        var names = NewNames();
        Assert.True(names.Set("AAA", "Turbo su Oro"));

        Assert.False(names.Set("AAA", "Turbo su Oro"));
        Assert.False(names.Set("AAA", "   "));
        Assert.False(names.Set("  ", "Turbo su Oro"));
        Assert.True(names.Set("AAA", "Turbo su Argento"));

        Assert.Equal("Turbo su Argento", names.Get("AAA"));
        Assert.Equal(1, names.Count);
    }

    [Fact]
    public void A_damaged_file_is_kept_aside_and_the_names_start_again()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "nomi-emittenti.json");
        File.WriteAllText(path, "{ rotto");

        var names = new IssuerNames(path);

        Assert.Equal(0, names.Count);
        Assert.True(File.Exists(path + ".rovinato"));
        Assert.True(names.Set("AAA", "Turbo su Oro"));
    }

    // ---------------------------------------------------------------- quale nome si vede

    private const string Header = "\"datetime\",\"date\",\"account_type\",\"category\",\"type\",\"asset_class\",\"name\",\"symbol\",\"shares\",\"price\",\"amount\",\"fee\",\"tax\",\"currency\",\"description\",\"transaction_id\"";

    [Fact]
    public async Task The_name_shown_is_the_users_then_the_issuers_then_the_one_from_the_file()
    {
        var previous = Environment.GetEnvironmentVariable("PELAGO_TEST");
        Environment.SetEnvironmentVariable("PELAGO_TEST", "1");
        Directory.CreateDirectory(_dir);
        try
        {
            var path = Path.Combine(_dir, "d.csv");
            File.WriteAllLines(path, new[]
            {
                Header,
                "\"2026-09-01T10:00:00.000Z\",\"2026-09-01\",\"DEFAULT\",\"TRADING\",\"BUY\",\"DERIVATIVE\",\"Long 4.100 $\",\"DE000FAAD001\",\"100\",\"1\",\"-50\",\"\",\"\",\"EUR\",\"Buy trade DE000FAAD001 Best Turbo auf GOLD, quantity: 100\",\"t1\"",
            });
            var issuerNames = NewNames();
            var userNames = new InstrumentNames(Path.Combine(_dir, "nomi-titoli.json"));
            using var state = new AppState(new UiPrefs(), userNames, issuerNames);
            Assert.True(await state.LoadFileAsync(path));
            const string key = "DE000FAAD001";

            // senza il nome dell'emittente: quello ricavato dal file
            Assert.Equal("Best Turbo auf GOLD (Long 4.100 $)", state.FullNameOf(key, "Long 4.100 $"));
            Assert.Null(state.IssuerNameOf(key));

            // con il nome dell'emittente: prende il posto di quello del file, e l'etichetta del file resta in coda per distinguere i prodotti
            issuerNames.Set(key, "Turbo Open-End su Oro");
            Assert.Equal("Turbo Open-End su Oro (Long 4.100 $)", state.FullNameOf(key, "Long 4.100 $"));
            Assert.Equal("Turbo Open-End su Oro (Long 4.100 $)", state.DefaultNameOf(key, ""));
            Assert.Equal("Best Turbo auf GOLD (Long 4.100 $)", state.OriginalNameOf(key, ""));

            // il nome scelto dall'utente vince su tutto, e tolto si torna a quello dell'emittente
            userNames.Set(key, "Il mio oro");
            Assert.Equal("Il mio oro", state.FullNameOf(key, "Long 4.100 $"));
            Assert.Equal("Il mio oro", state.ChosenNameOf(key));
            userNames.Set(key, "");
            Assert.Equal("Turbo Open-End su Oro (Long 4.100 $)", state.FullNameOf(key, "Long 4.100 $"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PELAGO_TEST", previous);
        }
    }

    // ---------------------------------------------------------------- l'aggiornamento: un lettore per emittente

    private sealed class FakeProvider : IDerivativeProvider
    {
        private readonly Func<string, DerivativeLookup> _lookup;

        public FakeProvider(string source, string issuerName, Func<string, DerivativeLookup> lookup)
        {
            Source = source;
            IssuerName = issuerName;
            _lookup = lookup;
        }

        public string Source { get; }
        public string IssuerName { get; }
        public List<string> Asked { get; } = new();
        public bool Disposed { get; private set; }

        public Task<DerivativeLookup> GetQuoteAsync(string isin, CancellationToken cancellationToken)
        {
            Asked.Add(isin);
            return Task.FromResult(_lookup(isin));
        }

        public void Dispose() => Disposed = true;
    }

    private static AssetCatalogEntry Entry(string isin) => new()
    {
        Key = isin,
        Name = "Long 100 $",
        Symbol = isin,
        AssetClass = "DERIVATIVE",
        FileName = isin + "-" + Guid.NewGuid().ToString("N") + ".json",
    };

    [Fact]
    public async Task Each_issuer_is_asked_by_its_own_reader_and_the_name_travels_with_the_price()
    {
        var vontobel = new FakeProvider("vontobel", "Vontobel", _ => new DerivativeLookup(DerivativeLookupStatus.Ok,
            new DerivativeQuote(0.33m, 0.33m, 0.34m, null, null, "EUR", ProductName: "Turbo su Acme Energy")));
        var vontobelEntry = Entry("DE000VAAA001");
        var otherEntry = Entry("DE000FAAD001");                  // di Société Générale: per questa prova non c'è un lettore
        var results = new List<AssetUpdateResult>();

        await AssetUpdateService.RunAsync(new List<AssetCatalogEntry> { vontobelEntry, otherEntry }, results.Add, CancellationToken.None,
            issuer => issuer == Issuer.Vontobel ? vontobel : null, derivativePauseMilliseconds: 0);

        Assert.Equal(new[] { "DE000VAAA001" }, vontobel.Asked);
        Assert.True(vontobel.Disposed);

        var ok = Assert.Single(results, r => r.Success);
        Assert.Equal("DE000VAAA001", ok.Entry.Key);
        Assert.Equal(0.33m, ok.Price);
        Assert.Equal("Turbo su Acme Energy", ok.ProductName);

        var unsupported = Assert.Single(results, r => !r.Success);
        Assert.Equal("DE000FAAD001", unsupported.Entry.Key);
        Assert.False(unsupported.Blocked);
    }

    [Fact]
    public async Task The_quote_is_stored_with_the_time_the_issuer_gives_when_there_is_one()
    {
        var tick = new DateTime(2026, 10, 9, 20, 0, 0, DateTimeKind.Utc);
        var provider = new FakeProvider("fonte-prova", "Emittente", _ => new DerivativeLookup(DerivativeLookupStatus.Ok,
            new DerivativeQuote(1.5m, 1.5m, 1.6m, null, tick, "EUR", StampUtc: tick)));
        var entry = Entry("DE000VAAB003");

        await AssetUpdateService.RunAsync(new List<AssetCatalogEntry> { entry }, _ => { }, CancellationToken.None, _ => provider, 0);

        var saved = Assert.Single(QuoteHistoryStore.Load(entry));
        Assert.Equal(tick, saved.Timestamp.ToUniversalTime());
        Assert.Equal("fonte-prova", saved.Source);
        Assert.Equal(1.5m, saved.Price);
    }

    [Fact]
    public async Task After_two_odd_replies_in_a_row_the_reader_stops_asking_that_issuer()
    {
        var provider = new FakeProvider("v", "Vontobel", _ => new DerivativeLookup(DerivativeLookupStatus.Blocked, HttpStatus: 429));
        var entries = new[] { "DE000VAAA001", "DE000VAAB003", "DE000VAAC004", "DE000BDAA002" }.Select(Entry).ToList();
        var results = new List<AssetUpdateResult>();

        await AssetUpdateService.RunAsync(entries, results.Add, CancellationToken.None, _ => provider, 0);

        Assert.Equal(2, provider.Asked.Count);
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.False(r.Success));
    }
}
