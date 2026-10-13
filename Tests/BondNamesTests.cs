using Pelago.Models;
using Pelago.Services;
using Xunit;

namespace Pelago.Tests;

public class BondCountryTests
{
    [Theory]
    [InlineData("IT00TESTBTP9", "IT")]
    [InlineData("de0001102580", "DE")]
    [InlineData("US912810TW83", "US")]
    public void A_bond_has_the_country_of_its_isin(string isin, string code)
    {
        Assert.Equal(code, Issuers.CountryCode(isin, "BOND"));
        Assert.Equal(Loc.T(GovBonds.Find(code)!.Name), Issuers.ShortName(isin, "BOND"));      // «Italia», nella lingua dell'app
    }

    [Theory]
    [InlineData("XS2134567890", "BOND")]      // eurobond: nessun paese
    [InlineData("IT00TESTBTP9", "STOCK")]     // azioni e fondi: il prefisso non dice chi emette
    [InlineData("IE00TEST0001", "FUND")]
    [InlineData("I", "BOND")]
    [InlineData(null, "BOND")]
    [InlineData("IT00TESTBTP9", null)]
    public void Anything_else_has_no_country(string? isin, string? assetClass)
    {
        Assert.Null(Issuers.CountryCode(isin, assetClass));
    }

    [Fact]
    public void A_derivative_still_has_its_bank_and_no_country()
    {
        Assert.Equal("Vontobel", Issuers.ShortName("DE000VAAA001", "DERIVATIVE"));
        Assert.Equal("Société Générale", Issuers.ShortName("DE000FAAA006", "DERIVATIVE"));
        Assert.Null(Issuers.CountryCode("DE000VAAA001", "DERIVATIVE"));
    }
}

[Collection("AppState")]
public class BondNamesTests
{
    private const string Header = "\"datetime\",\"date\",\"account_type\",\"category\",\"type\",\"asset_class\",\"name\",\"symbol\",\"shares\",\"price\",\"amount\",\"fee\",\"tax\",\"currency\",\"description\",\"transaction_id\"";

    private static string Trade(string cls, string name, string symbol, string id) =>
        $"\"2026-09-01T10:00:00.000Z\",\"2026-09-01\",\"DEFAULT\",\"TRADING\",\"BUY\",\"{cls}\",\"{name}\",\"{symbol}\",\"1000\",\"1\",\"-1000\",\"\",\"\",\"EUR\",\"Buy trade {symbol}\",\"{id}\"";

    private static BtpBond Bond(string isin, string description) => new(isin, description, 0.043m, new DateOnly(2054, 10, 1), 100m);

    private sealed class Rig : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "pelago-bond-" + Guid.NewGuid().ToString("N"));
        private readonly string? _previousTest = Environment.GetEnvironmentVariable("PELAGO_TEST");

        public AppState State { get; }

        public Rig(params string[] rows)
        {
            Directory.CreateDirectory(_folder);
            Environment.SetEnvironmentVariable("PELAGO_TEST", "1");
            Csv = Path.Combine(_folder, "b.csv");
            File.WriteAllLines(Csv, new[] { Header }.Concat(rows));
            State = new AppState(new UiPrefs(), new InstrumentNames(Path.Combine(_folder, "nomi-titoli.json")), new IssuerNames(Path.Combine(_folder, "nomi-emittenti.json")));
        }

        public string Csv { get; }

        public void Dispose()
        {
            State.Dispose();
            Environment.SetEnvironmentVariable("PELAGO_TEST", _previousTest);
            File.Delete(Path.Combine(DataLocations.Root, "btp.json"));
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_btp_of_the_account_takes_the_name_of_borsa_italiana_without_the_label_of_the_file()
    {
        using var rig = new Rig(Trade("BOND", "Okt. 2054", "IT00TESTBTP1", "t1"), Trade("DERIVATIVE", "Long 150,00 $", "DE000VAAA001", "t2"));
        Assert.True(await rig.State.LoadFileAsync(rig.Csv));

        var found = rig.State.LearnBondNames(new[] { Bond("IT00TESTBTP1", "BTP 4,3% Ott54"), Bond("IT0000000001", "BTP di un altro") });

        Assert.Equal(1, found);                                                                // solo i BTP che ho nel conto
        Assert.Equal("BTP 4,3% Ott54", rig.State.IssuerNameOf("IT00TESTBTP1"));                // niente «(Okt. 2054)» in coda
        Assert.Equal("BTP 4,3% Ott54", rig.State.FullNameOf("IT00TESTBTP1", "Okt. 2054"));
        Assert.Null(rig.State.IssuerNameOf("IT0000000001"));

        // il nome scelto dall'utente vince, come per i derivati
        rig.State.Names.Set("IT00TESTBTP1", "Il mio BTP lungo");
        Assert.Equal("Il mio BTP lungo", rig.State.FullNameOf("IT00TESTBTP1", "Okt. 2054"));

        // per un derivato non cambia nulla: il nome dell'emittente porta ancora l'etichetta del file
        rig.State.LearnBondNames(Array.Empty<BtpBond>());
        Assert.Null(rig.State.IssuerNameOf("DE000VAAA001"));
    }

    [Fact]
    public async Task Updating_everything_downloads_the_btp_list_once_and_remembers_the_names()
    {
        using var rig = new Rig(Trade("BOND", "Okt. 2054", "IT00TESTBTP1", "t1"));
        Assert.True(await rig.State.LoadFileAsync(rig.Csv));
        var calls = 0;
        rig.State.BtpFetcher = _ =>
        {
            calls++;
            return Task.FromResult(new BtpFetchResult(new List<BtpBond> { Bond("IT00TESTBTP1", "BTP 4,3% Ott54"), Bond("IT0000000001", "BTP di un altro") }, 2, 0, new List<string>()));
        };

        await rig.State.RunUpdateAsync(new[] { "BOND" }, "BTP");

        Assert.Equal(1, calls);                                                                // una richiesta sola
        Assert.Equal("BTP 4,3% Ott54", rig.State.IssuerNameOf("IT00TESTBTP1"));
        Assert.Equal(2, BtpStore.Load().Bonds.Count);                                          // e l'elenco è salvato come dal pulsante della pagina dei BTP
        Assert.Contains(rig.State.UpdateLog, line => line.Contains("OK") && line.Contains("BTP"));
    }

    [Fact]
    public async Task Without_italian_bonds_in_the_account_the_site_is_not_bothered()
    {
        using var rig = new Rig(Trade("BOND", "Mai 2034", "DE0001102580", "t1"), Trade("FUND", "Fondo", "IE00TEST0001", "t2"));
        Assert.True(await rig.State.LoadFileAsync(rig.Csv));
        var calls = 0;
        rig.State.BtpFetcher = _ =>
        {
            calls++;
            return Task.FromResult(new BtpFetchResult(new List<BtpBond>(), 0, 0, new List<string>()));
        };

        await rig.State.RunUpdateAsync(new[] { "BOND" }, "BTP");

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task A_failed_download_is_logged_and_changes_nothing()
    {
        using var rig = new Rig(Trade("BOND", "Okt. 2054", "IT00TESTBTP1", "t1"));
        Assert.True(await rig.State.LoadFileAsync(rig.Csv));
        rig.State.BtpFetcher = _ => throw new HttpRequestException("sito non raggiungibile");

        await rig.State.RunUpdateAsync(new[] { "BOND" }, "BTP");

        Assert.Null(rig.State.IssuerNameOf("IT00TESTBTP1"));
        Assert.Contains(rig.State.UpdateLog, line => line.Contains("sito non raggiungibile"));
        Assert.False(rig.State.IsUpdating);
    }
}
