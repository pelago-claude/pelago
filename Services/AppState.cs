using System.Globalization;
using Pelago.Models;
using static Pelago.Services.Loc;

namespace Pelago.Services;

/// <summary>
/// Stato condiviso dell'interfaccia: operazioni caricate dal CSV, andamento giornaliero, quotazioni, controlli di coerenza e
/// registro dell'aggiornamento in corso. Si occupa anche delle attività "da sole": riapre l'ultimo file, ricarica il CSV se
/// cambia, aggiorna i prezzi se sono vecchi, ripara i prezzi in valuta estera e fa il backup settimanale.
/// Le pagine si iscrivono a <see cref="Changed"/> / <see cref="UpdateLogChanged"/> / <see cref="Toast"/>.
/// </summary>
public sealed class AppState : IDisposable
{
    /// <summary>«BOND» non è una categoria di prezzi: vuol dire «scarica anche l'elenco dei BTP da Borsa Italiana», se nel conto ci sono BTP.</summary>
    public static readonly string[] AllSupportedAssetClasses = { "CRYPTO", "STOCK", "FUND", "FX", "DERIVATIVE", "BOND" };

    /// <summary>All'apertura i prezzi vengono aggiornati da soli se l'ultimo aggiornamento è più vecchio di così.</summary>
    public const int AutoUpdateAfterHours = 12;

    private readonly UiPrefs _prefs;
    private readonly object _logLock = new();
    private readonly List<string> _updateLog = new();

    // Nelle prove automatiche (PELAGO_TEST=1) niente attività in rete o su disco che non siano state chieste.
    // Anche nel conto di esempio: niente aggiornamenti dei prezzi né backup che nessuno ha chiesto.
    private readonly bool _quiet = (Environment.GetEnvironmentVariable("PELAGO_TEST") == "1" && Environment.GetEnvironmentVariable("PELAGO_AUTO") != "1")
        || DemoAccount.IsActive;

    private bool _initialized;
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounce;
    private string? _candidate;

    private readonly InstrumentNames _names;
    private readonly IssuerNames _issuerNames;

    public AppState(UiPrefs prefs, InstrumentNames? names = null, IssuerNames? issuerNames = null)
    {
        _prefs = prefs;
        _names = names ?? new InstrumentNames();
        _issuerNames = issuerNames ?? new IssuerNames();
        Loc.Changed += OnLanguageChanged;
        _names.Changed += OnNamesChanged;
    }

    /// <summary>I nomi che l'utente ha dato ai titoli (vedi <see cref="InstrumentNames"/>).</summary>
    public InstrumentNames Names => _names;

    /// <summary>I nomi dei derivati letti dai siti degli emittenti (vedi <see cref="IssuerNames"/>).</summary>
    public IssuerNames IssuerNames => _issuerNames;

    /// <summary>Cambia un nome: le pagine e i controlli (che citano i titoli per nome) vanno rifatti.</summary>
    private void OnNamesChanged()
    {
        if (Operations is not null)
        {
            RecomputeDerived();
        }
        Changed?.Invoke();
    }

    /// <summary>Cambiando lingua i testi dei controlli di coerenza vanno rifatti nella nuova lingua.</summary>
    private void OnLanguageChanged()
    {
        if (Operations is not null)
        {
            RecomputeDerived();
        }
        Changed?.Invoke();
    }

    public string? LoadedFilePath { get; private set; }
    public List<Operation>? Operations { get; private set; }
    public List<DailySnapshot> Snapshots { get; private set; } = new();
    public IReadOnlyDictionary<string, List<(DateTime Timestamp, decimal Price)>> Quotes { get; private set; }
        = new Dictionary<string, List<(DateTime, decimal)>>();

    /// <summary>Tutti gli asset movimentati, con quantità e ultimo prezzo noto.</summary>
    public IReadOnlyList<Holding> Holdings { get; private set; } = Array.Empty<Holding>();

    /// <summary>Quantità e costo medio ponderato delle posizioni ancora aperte, per chiave dell'asset.</summary>
    public IReadOnlyDictionary<string, OpenPosition> OpenPositions { get; private set; } = new Dictionary<string, OpenPosition>();

    /// <summary>Esiti dei controlli di coerenza (vedi <see cref="ConsistencyChecker"/>).</summary>
    public IReadOnlyList<Finding> Findings { get; private set; } = Array.Empty<Finding>();

    public int WarningCount => Findings.Count(f => f.Level == FindingLevel.Warning);

    /// <summary>Nome "intero" di ogni asset (dal testo delle operazioni), per le tabelle riassuntive.</summary>
    public IReadOnlyDictionary<string, string> FullNames { get; private set; } = new Dictionary<string, string>();

    public string? LoadError { get; private set; }

    /// <summary>Questa finestra è il conto di esempio (dati inventati, in una cartella a parte), non i dati dell'utente.</summary>
    public bool IsDemo => DemoAccount.IsActive;

    // «Ho capito» vale solo per questa esecuzione del programma: non si salva da nessuna parte, quindi a ogni avvio la finestra ricompare.
    // Le prove automatiche (PELAGO_TEST=1 con PELAGO_TAX_ACCEPTED=1) la saltano, altrimenti coprirebbe le pagine che devono fotografare.
    private bool _taxDisclaimerAccepted = Environment.GetEnvironmentVariable("PELAGO_TEST") == "1" && Environment.GetEnvironmentVariable("PELAGO_TAX_ACCEPTED") == "1";

    /// <summary>
    /// La finestra con l'avvertenza sulla parte fiscale (Zainetto fiscale, Costi e tasse, Cedole e scadenze) si mostra la prima volta che se ne apre una pagina
    /// dopo l'avvio del programma, con un file caricato (senza dati non c'è nulla che si possa usare male): finché l'utente non preme «Ho capito».
    /// Navigando altrove e tornando non ricompare; chiudendo e riaprendo il programma sì, anche subito.
    /// </summary>
    public bool TaxDisclaimerPending => HasData && !_taxDisclaimerAccepted;

    public void AcceptTaxDisclaimer()
    {
        _taxDisclaimerAccepted = true;
        Changed?.Invoke();
    }

    public bool IsLoading { get; private set; }
    public bool IsUpdating { get; private set; }
    public string? UpdateTitle { get; private set; }

    /// <summary>Un CSV più recente trovato nella cartella del file aperto, in attesa che l'utente decida se aprirlo.</summary>
    public string? PendingFile { get; private set; }

    /// <summary>L'app sta usando lo storico delle operazioni (l'unione dei CSV aggiunti) e non un singolo file.</summary>
    public bool IsArchive => OperationArchive.IsArchive(LoadedFilePath);

    /// <summary>Il nuovo CSV in attesa va <i>aggiunto</i> allo storico (e non aperto al posto del file attuale).</summary>
    public bool PendingIsAddition => PendingFile is not null && IsArchive;

    /// <summary>L'esito dell'ultima aggiunta di movimenti, da mostrare finché l'utente non lo chiude.</summary>
    public MergeReport? LastMerge { get; private set; }

    private DateTime _suppressFileEventsUntilUtc;
    private bool _merging;

    public bool HasData => Operations is { Count: > 0 };

    /// <summary>Cambiano i dati caricati o le quotazioni: le pagine devono ricalcolarsi.</summary>
    public event Action? Changed;

    /// <summary>È cambiato il registro dell'aggiornamento quotazioni (può arrivare da un thread in background).</summary>
    public event Action? UpdateLogChanged;

    /// <summary>Un breve messaggio da mostrare all'utente (può arrivare da un thread in background).</summary>
    public event Action<string>? Toast;

    public void Notify(string message) => Toast?.Invoke(message);

    // ---------------------------------------------------------------- impostazioni

    public bool AutoUpdateEnabled
    {
        get => _prefs.Get("auto.quotes") != "0";
        set { _prefs.Set("auto.quotes", value ? "1" : "0"); Changed?.Invoke(); }
    }

    /// <summary>
    /// Legge dal sito di Vontobel il prezzo dei derivati aperti (vedi <see cref="VontobelQuoteProvider"/>). Spenta di default: la accende l'utente, dopo aver letto le condizioni
    /// del sito (uso privato non commerciale, sito rivolto a chi risiede in Germania, Austria, Lussemburgo e Liechtenstein).
    /// </summary>
    public bool VontobelEnabled
    {
        get => _prefs.Get("quotes.vontobel") == "1";
        set { _prefs.Set("quotes.vontobel", value ? "1" : "0"); Changed?.Invoke(); }
    }

    /// <summary>
    /// Legge dal sito di Société Générale il prezzo dei derivati aperti (vedi <see cref="SocieteGeneraleQuoteProvider"/>). Spenta di default e separata da quella di Vontobel:
    /// le Note Legali del sito vietano la riproduzione dei contenuti con qualsiasi mezzo senza autorizzazione scritta, quindi la accende l'utente, sotto la sua responsabilità.
    /// </summary>
    public bool SocGenEnabled
    {
        get => _prefs.Get("quotes.socgen") == "1";
        set { _prefs.Set("quotes.socgen", value ? "1" : "0"); Changed?.Invoke(); }
    }

    /// <summary>La lettura dei prezzi di questo emittente è accesa?</summary>
    public bool IsEnabled(Issuer issuer) => issuer switch
    {
        Issuer.Vontobel => VontobelEnabled,
        Issuer.SocieteGenerale => SocGenEnabled,
        _ => false,
    };

    /// <summary>I derivati aperti di tutti gli emittenti la cui lettura è accesa: sono quelli che l'aggiornamento va a leggere.</summary>
    public HashSet<string> OpenDerivativesToRead()
    {
        var keys = new HashSet<string>();
        foreach (var issuer in Enum.GetValues<Issuer>().Where(IsEnabled))
        {
            keys.UnionWith(OpenDerivatives(issuer));
        }
        return keys;
    }

    /// <summary>I derivati con quantità aperta di un emittente (per chiave del titolo).</summary>
    public HashSet<string> OpenDerivatives(Issuer issuer) => Holdings
        .Where(h => h.AssetClass == "DERIVATIVE" && h.Shares > 0m && Issuers.OfIsin(h.Symbol) == issuer)
        .Select(h => h.Key).ToHashSet();

    /// <summary>I derivati con quantità aperta di cui si può leggere il prezzo da Vontobel (per chiave del titolo).</summary>
    public HashSet<string> OpenVontobelDerivatives() => OpenDerivatives(Issuer.Vontobel);

    public bool AutoBackupEnabled
    {
        get => _prefs.Get("auto.backup") != "0";
        set { _prefs.Set("auto.backup", value ? "1" : "0"); Changed?.Invoke(); }
    }

    public string BackupFolder
    {
        get => _prefs.Get("backup.folder") is { Length: > 0 } f ? f : BackupService.DefaultFolder;
        set { _prefs.Set("backup.folder", value); Changed?.Invoke(); }
    }

    /// <summary>Obiettivo di patrimonio (euro), se impostato.</summary>
    public decimal? Goal => decimal.TryParse(_prefs.Get("goal.amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var g) && g > 0m ? g : null;

    public void SetGoal(decimal? amount)
    {
        _prefs.Set("goal.amount", amount is > 0m ? amount.Value.ToString(CultureInfo.InvariantCulture) : "");
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- date e viste

    public DateOnly MinDate => Operations!.Min(o => o.Date);

    /// <summary>Oggi, o l'ultima operazione se successiva (il calendario non deve escludere dati reali).</summary>
    public DateOnly MaxDate
    {
        get
        {
            var last = Operations!.Max(o => o.Date);
            var today = DateOnly.FromDateTime(DateTime.Today);
            return today > last ? today : last;
        }
    }

    private DateOnly? _viewDate;

    /// <summary>
    /// Il giorno che si sta guardando con lo slider del tempo: lo condividono la Mappa del portafoglio e Composizione a una data, così passando dall'una
    /// all'altra non va reimpostato. Resta sempre tra il primo e l'ultimo giorno dei dati (oggi, se non si è mai mosso lo slider). Non fa ridisegnare le pagine.
    /// </summary>
    public DateOnly ViewDate
    {
        get
        {
            if (Operations is null)
            {
                return default;
            }
            var date = _viewDate ?? MaxDate;
            return date < MinDate ? MinDate : date > MaxDate ? MaxDate : date;
        }
        set => _viewDate = value;
    }

    public DailySnapshot? Latest => Snapshots.Count == 0 ? null : Snapshots.OrderByDescending(s => s.Date).First();

    /// <summary>Data dell'ultima quotazione registrata (dai dati, non dalla data dei file: una riparazione o un backup riscrivono i file senza aggiornare nulla).</summary>
    public DateTime? QuotesLastUpdated => _lastQuoteUtc?.ToLocalTime();

    private DateTime? _lastQuoteUtc;

    private void SetQuotes(Dictionary<string, List<(DateTime Timestamp, decimal Price)>> quotes)
    {
        Quotes = quotes;
        var latest = quotes.Values.SelectMany(v => v).Select(p => p.Timestamp).DefaultIfEmpty().Max();
        _lastQuoteUtc = latest == default ? null : DateTime.SpecifyKind(latest, DateTimeKind.Utc);
    }

    /// <summary>
    /// Il nome da mostrare per un titolo: quello scelto dall'utente, altrimenti (per i derivati) quello dell'emittente, altrimenti il nome intero ricavato
    /// dalle operazioni, altrimenti <paramref name="fallback"/>.
    /// </summary>
    public string FullNameOf(string key, string fallback) => ChosenNameOf(key) ?? OriginalNameOf(key, fallback);

    /// <summary>Il nome scelto dall'utente o, in mancanza, quello dell'emittente; null se non c'è né l'uno né l'altro (si userà il nome del file).</summary>
    public string? ChosenNameOf(string key) => _names.Get(key) ?? IssuerNameOf(key);

    /// <summary>Il nome del titolo senza quello scelto dall'utente: quello dell'emittente se lo si conosce, altrimenti quello del file.</summary>
    public string DefaultNameOf(string key, string fallback) => IssuerNameOf(key) ?? OriginalNameOf(key, fallback);

    /// <summary>
    /// Il nome di un derivato secondo l'emittente («Turbo su Oro»), con in coda l'etichetta del file («Long 4.100 $») che distingue un prodotto dall'altro
    /// sullo stesso titolo. Per un BTP il nome di Borsa Italiana («BTP 4,3% Ott54») ha già cedola e scadenza: è unico, e non si aggiunge nulla.
    /// Null se non lo si è ancora letto dal sito.
    /// </summary>
    public string? IssuerNameOf(string key)
    {
        var product = _issuerNames.Get(key);
        if (product is null)
        {
            return null;
        }
        if (BondKeys.Contains(InstrumentNames.Normalize(key)))
        {
            return product;
        }
        return ShortNames.TryGetValue(key, out var shortName) && !string.IsNullOrWhiteSpace(shortName) ? $"{product} ({shortName})" : product;
    }

    /// <summary>
    /// Ricorda il nome che Borsa Italiana dà ai BTP che sono nel conto («Okt. 2054» nel file del broker → «BTP 4,3% Ott54»): da allora le liste usano
    /// quello, tranne dove l'utente ha scelto un nome suo. Restituisce quanti BTP del conto sono stati trovati nell'elenco.
    /// </summary>
    public int LearnBondNames(IEnumerable<BtpBond> bonds)
    {
        var mine = BondKeys;
        if (mine.Count == 0)
        {
            return 0;
        }

        var found = 0;
        var changed = false;
        foreach (var bond in bonds)
        {
            if (mine.Contains(InstrumentNames.Normalize(bond.Isin)) && !string.IsNullOrWhiteSpace(bond.Description))
            {
                found++;
                changed |= _issuerNames.Set(bond.Isin, bond.Description);
            }
        }
        if (changed)
        {
            Changed?.Invoke();
        }
        return found;
    }

    /// <summary>Chi scarica l'elenco dei BTP (Borsa Italiana): per le prove automatiche si può sostituire con uno finto.</summary>
    public Func<CancellationToken, Task<BtpFetchResult>> BtpFetcher { get; set; } = BtpOnlineProvider.FetchAsync;

    /// <summary>I codici delle obbligazioni che compaiono nelle operazioni.</summary>
    private HashSet<string> BondKeys { get; set; } = new(StringComparer.Ordinal);

    private static HashSet<string> BuildBondKeys(List<Operation> operations) => operations
        .Where(o => o.AssetClass == "BOND" && !string.IsNullOrWhiteSpace(o.PositionKey))
        .Select(o => InstrumentNames.Normalize(o.PositionKey))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Il nome del titolo nel file (senza il nome scelto dall'utente né quello dell'emittente).</summary>
    public string OriginalNameOf(string key, string fallback) => FullNames.TryGetValue(key, out var n) ? n : fallback;

    /// <summary>Il nome breve di ogni asset come sta nel file del broker (per i derivati l'etichetta «Long 150,00 $»).</summary>
    private IReadOnlyDictionary<string, string> ShortNames { get; set; } = new Dictionary<string, string>();

    public IReadOnlyList<string> UpdateLog
    {
        get
        {
            lock (_logLock)
            {
                return _updateLog.ToList();
            }
        }
    }

    // ---------------------------------------------------------------- caricamento

    /// <summary>Alla prima apertura riapre l'ultimo CSV usato, se esiste ancora; poi avvia le attività automatiche.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        var last = RecentFileStore.GetLastFilePath();
        if (last is not null && File.Exists(last))
        {
            await LoadFileAsync(last);
        }

        // Solo per le prove automatiche: aggiunge subito un CSV (come farebbe «Aggiungi movimenti…»), per fotografare l'esito
        if (Environment.GetEnvironmentVariable("PELAGO_TEST_ADD") is { Length: > 0 } testAdd && File.Exists(testAdd))
        {
            await AddMovementsAsync(testAdd);
        }

        if (!_quiet && AutoBackupEnabled)
        {
            _ = Task.Run(RunBackupIfDue);
        }
    }

    public async Task<bool> LoadFileAsync(string path)
    {
        IsLoading = true;
        LoadError = null;
        PendingFile = null;
        Changed?.Invoke();

        try
        {
            var (operations, quotes, snapshots) = await Task.Run(() =>
            {
                var ops = CsvOperationReader.Read(path);
                if (ops.Count == 0)
                {
                    throw new InvalidDataException(T("Il file non contiene operazioni."));
                }
                var q = QuoteHistoryStore.LoadAllAsTimelines(AssetCatalogStore.Load());
                return (ops, q, PortfolioCalculator.Compute(ops, q));
            });

            Operations = operations;
            SetQuotes(quotes);
            Snapshots = snapshots;
            FullNames = BuildFullNames(operations);
            ShortNames = BuildShortNames(operations);
            BondKeys = BuildBondKeys(operations);
            RecomputeDerived();
            LoadedFilePath = path;
            RecentFileStore.SetLastFilePath(path);
            _prefs.Set("import.folder", IsArchive ? _prefs.Get("import.folder") ?? "" : Path.GetDirectoryName(path) ?? "");
            StartWatching(IsArchive ? _prefs.Get("import.folder") : Path.GetDirectoryName(path));
            _ = Task.Run(RepairQuotesAsync);
            MaybeAutoUpdate();
            return true;
        }
        catch (Exception ex)
        {
            LoadError = T("Impossibile leggere il file: {0}", ex.Message);
            return false;
        }
        finally
        {
            IsLoading = false;
            Changed?.Invoke();
        }
    }

    public void ReloadQuotes()
    {
        SetQuotes(QuoteHistoryStore.LoadAllAsTimelines(AssetCatalogStore.Load()));
        if (Operations is not null)
        {
            Snapshots = PortfolioCalculator.Compute(Operations, Quotes);
            RecomputeDerived();
        }
        Changed?.Invoke();
    }

    private void RecomputeDerived()
    {
        if (Operations is null)
        {
            return;
        }

        var holdings = PortfolioCalculator.ComputeHoldings(Operations, Quotes);
        Holdings = holdings;
        OpenPositions = RealizedGainsCalculator.ComputeOpenPositions(Operations);
        Findings = ConsistencyChecker.Run(Operations, Snapshots, Quotes, AssetCatalogStore.Load(), holdings,
            QuoteHistoryStore.Load, k => FullNameOf(k, k), DateTime.Now);
    }

    private static Dictionary<string, string> BuildFullNames(List<Operation> operations) => operations
        .Where(o => !string.IsNullOrWhiteSpace(o.PositionKey) && !string.IsNullOrWhiteSpace(o.AssetClass))
        .GroupBy(o => o.PositionKey)
        .ToDictionary(g => g.Key, g =>
        {
            var newest = g.OrderByDescending(o => o.DateTime).ToList();
            return AssetNameResolver.ResolveFullName(newest[0].Name, newest[0].AssetClass, newest.Select(o => o.Description));
        });

    private static Dictionary<string, string> BuildShortNames(List<Operation> operations) => operations
        .Where(o => !string.IsNullOrWhiteSpace(o.PositionKey) && !string.IsNullOrWhiteSpace(o.AssetClass))
        .GroupBy(o => o.PositionKey)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.DateTime).First().Name);

    /// <summary>Converte in euro eventuali prezzi salvati in passato in un'altra valuta (vedi <see cref="QuoteRepairService"/>).</summary>
    private async Task RepairQuotesAsync()
    {
        try
        {
            var repaired = await QuoteRepairService.RepairAsync(AssetCatalogStore.Load(), AppendLog, CancellationToken.None);
            if (repaired > 0)
            {
                ReloadQuotes();
                Notify(T("Corretti {0} prezzi che erano in valuta estera (ora sono in euro).", repaired));
            }
        }
        catch (Exception ex)
        {
            AppendLog(T("Riparazione prezzi non riuscita: {0}", ex.Message));
        }
    }

    // ---------------------------------------------------------------- attività automatiche

    private void MaybeAutoUpdate()
    {
        if (_quiet || !AutoUpdateEnabled || IsUpdating || Operations is null)
        {
            return;
        }

        var last = _lastQuoteUtc;
        if (last is null || DateTime.UtcNow - last.Value > TimeSpan.FromHours(AutoUpdateAfterHours))
        {
            _ = RunUpdateAsync(AllSupportedAssetClasses, "aggiornamento automatico all'apertura", automatic: true);
        }
    }

    private void RunBackupIfDue()
    {
        try
        {
            var result = BackupService.RunIfDue(_prefs, BackupFolder, DateTime.Now);
            if (result is not null)
            {
                Notify(T("Backup automatico salvato: {0}", result));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notify(T("Backup automatico non riuscito: {0}", ex.Message));
        }
    }

    // ---------------------------------------------------------------- controllo del file CSV

    private void StartWatching(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            _watcher?.Dispose();
            _watcher = null;
            return;
        }
        if (_watcher is not null && string.Equals(_watcher.Path, dir, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(dir, "*.csv")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _candidate = e.FullPath;
        _debounce?.Dispose();
        // Aspetta che il programma che scrive il file abbia finito.
        _debounce = new System.Threading.Timer(_ => _ = HandleFileEventAsync(), null, 1500, Timeout.Infinite);
    }

    private async Task HandleFileEventAsync()
    {
        try
        {
            var candidate = _candidate;
            if (candidate is null || !File.Exists(candidate) || IsLoading || DateTime.UtcNow < _suppressFileEventsUntilUtc)
            {
                return;
            }

            if (IsArchive)
            {
                // con lo storico, ogni nuovo CSV esportato nella cartella da cui si importa si può aggiungere: lo propone solo se più recente dell'ultimo aggiunto
                long.TryParse(_prefs.Get("import.stamp"), out var stamp);
                if (!CsvOperationReader.LooksLikeExport(candidate) || File.GetLastWriteTimeUtc(candidate).Ticks <= stamp)
                {
                    return;
                }
                PendingFile = candidate;
                Changed?.Invoke();
                return;
            }

            if (string.Equals(candidate, LoadedFilePath, StringComparison.OrdinalIgnoreCase))
            {
                if (await LoadFileAsync(candidate))
                {
                    Notify(T("Il file CSV è cambiato: dati ricaricati."));
                }
                return;
            }

            if (!CsvOperationReader.LooksLikeExport(candidate))
            {
                return;
            }
            if (LoadedFilePath is not null && File.Exists(LoadedFilePath)
                && File.GetLastWriteTimeUtc(candidate) <= File.GetLastWriteTimeUtc(LoadedFilePath))
            {
                return;
            }

            PendingFile = candidate;
            Changed?.Invoke();
        }
        catch (IOException)
        {
            // File ancora in uso da chi lo sta scrivendo: arriverà un altro evento.
        }
    }

    public async Task AcceptPendingFileAsync()
    {
        if (PendingFile is { } path)
        {
            if (IsArchive)
            {
                await AddMovementsAsync(path);
            }
            else
            {
                await LoadFileAsync(path);
            }
        }
    }

    // ---------------------------------------------------------------- storico delle operazioni

    /// <summary>
    /// Aggiunge i movimenti di un CSV allo storico (creandolo dal file oggi aperto se non esiste), scartando quelli già presenti, e passa a usare lo storico.
    /// L'esito resta in <see cref="LastMerge"/>.
    /// </summary>
    public async Task<bool> AddMovementsAsync(string csvPath)
    {
        if (_merging)
        {
            Notify(T("Un'aggiunta di movimenti è già in corso."));
            return false;
        }

        _merging = true;
        try
        {
            _suppressFileEventsUntilUtc = DateTime.UtcNow.AddSeconds(8);
            var seed = LoadedFilePath is { } open && !IsArchive ? open : null;
            var report = await Task.Run(() => OperationArchive.Merge(OperationArchive.ArchivePath, seed, csvPath));
            _prefs.Set("import.folder", Path.GetDirectoryName(csvPath) ?? "");
            _prefs.Set("import.stamp", File.GetLastWriteTimeUtc(csvPath).Ticks.ToString(CultureInfo.InvariantCulture));

            if (!await LoadFileAsync(OperationArchive.ArchivePath))
            {
                return false;
            }

            LastMerge = report;
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Notify(T("Impossibile aggiungere i movimenti: {0}", ex.Message));
            return false;
        }
        finally
        {
            _merging = false;
        }
    }

    /// <summary>Passa a usare lo storico delle operazioni (se esiste).</summary>
    public async Task<bool> UseArchiveAsync() =>
        File.Exists(OperationArchive.ArchivePath) && await LoadFileAsync(OperationArchive.ArchivePath);

    public void DismissMerge()
    {
        LastMerge = null;
        Changed?.Invoke();
    }

    public void DismissPendingFile()
    {
        PendingFile = null;
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- aggiornamento quotazioni

    /// <summary>Il nome di un asset nel registro: quello intero (per i derivati il nome del file è solo «Long 150,00 $»), accorciato a 44 caratteri.</summary>
    private string LogName(Pelago.Models.AssetCatalogEntry entry)
    {
        var name = FullNameOf(entry.Key, entry.Name);
        return name.Length > 44 ? name[..43] + "…" : name;
    }

    private void AppendLog(string line)
    {
        lock (_logLock)
        {
            _updateLog.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
        }
        UpdateLogChanged?.Invoke();
    }

    public void ClearUpdateLog()
    {
        lock (_logLock)
        {
            _updateLog.Clear();
        }
        UpdateLogChanged?.Invoke();
    }

    /// <summary>Aggiorna le quotazioni delle classi indicate (CRYPTO, STOCK, FUND, FX, DERIVATIVE) in parallelo (i derivati uno alla volta), riportando l'avanzamento nel registro.</summary>
    public async Task RunUpdateAsync(string[] assetClasses, string label, bool automatic = false)
    {
        if (IsUpdating)
        {
            return;
        }
        if (Operations is null)
        {
            AppendLog(T("Carica prima un file CSV: servono le operazioni per sapere quali asset aggiornare."));
            return;
        }

        IsUpdating = true;
        UpdateTitle = T(label);
        Changed?.Invoke();
        AppendLog(T("--- Avvio aggiornamento: {0} ---", T(label)));

        try
        {
            var withBtp = assetClasses.Contains("BOND");
            if (withBtp)
            {
                await UpdateBtpListAsync();
            }

            // i derivati si leggono solo se la funzione è accesa per il loro emittente e solo per quelli aperti: sono pochi, e le richieste ai siti vanno tenute al minimo
            var derivativeKeys = OpenDerivativesToRead();
            foreach (var assetClass in assetClasses.Where(ac => ac != "FX" && ac != "BOND"))
            {
                if (assetClass == "DERIVATIVE")
                {
                    if (derivativeKeys.Count > 0)
                    {
                        AssetCatalogStore.SyncFromOperations(
                            Operations.Where(o => o.AssetClass != "DERIVATIVE" || derivativeKeys.Contains(o.PositionKey)).ToList(), "DERIVATIVE");
                    }
                    continue;
                }
                AssetCatalogStore.SyncFromOperations(Operations, assetClass);
            }
            if (assetClasses.Contains("FX"))
            {
                AssetCatalogStore.EnsureFxEntry();
            }

            var entries = AssetCatalogStore.Load()
                .Where(e => e.AssetClass != "BOND" && assetClasses.Contains(e.AssetClass) && (e.AssetClass != "DERIVATIVE" || derivativeKeys.Contains(e.Key)))
                .ToList();
            if (entries.Count == 0)
            {
                if (!withBtp)
                {
                    AppendLog(T("Nessun asset da aggiornare per questa categoria."));
                }
                return;
            }

            AppendLog(T("{0} asset da aggiornare (fino a 5 in parallelo)...", entries.Count));
            var ok = 0;
            var failed = 0;

            await AssetUpdateService.RunAsync(entries, result =>
            {
                if (result.Success)
                {
                    Interlocked.Increment(ref ok);
                    if (result.ProductName is { } productName)
                    {
                        // il nome arriva insieme al prezzo (stessa pagina): lo si ricorda, e le liste lo usano al posto di quello del file
                        _issuerNames.Set(result.Entry.Key, productName);
                    }
                    var note = result.Message == "OK" ? "" : $"  [{result.Message}]";
                    AppendLog($"OK      {LogName(result.Entry),-26} ({result.Entry.Symbol}) -> {result.Price:F4} EUR{note}");
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    AppendLog($"{T("ERRORE"),-8}{LogName(result.Entry),-26} ({result.Entry.Symbol}): {result.Message}");
                }
            }, CancellationToken.None);

            AppendLog(T("--- Completato: {0} riusciti, {1} falliti ---", ok, failed));
            await Task.Run(ReloadQuotes);

            if (automatic)
            {
                Notify(failed == 0
                    ? T("Prezzi aggiornati da soli: {0} asset.", ok)
                    : T("Prezzi aggiornati da soli: {0} riusciti, {1} falliti (dettagli in Quotazioni).", ok, failed));
            }
        }
        catch (Exception ex)
        {
            AppendLog(T("ERRORE aggiornamento: {0}", ex.Message));
        }
        finally
        {
            IsUpdating = false;
            UpdateTitle = null;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Scarica l'elenco dei BTP da Borsa Italiana (una richiesta sola, come dal pulsante nella pagina dei BTP), lo salva e ne ricorda i nomi per i BTP del
    /// conto. Si fa solo se nel conto c'è almeno un titolo italiano tra le obbligazioni: senza BTP non c'è motivo di disturbare il sito.
    /// </summary>
    private async Task UpdateBtpListAsync()
    {
        if (!BondKeys.Any(k => k.StartsWith("IT", StringComparison.Ordinal)))
        {
            return;
        }

        AppendLog(T("Scarico l'elenco dei BTP da Borsa Italiana…"));
        try
        {
            var result = await BtpFetcher(CancellationToken.None);
            var data = BtpStore.Load();
            data.Bonds = result.Bonds;
            data.UpdatedAt = DateTime.Now;
            data.Source = BtpOnlineProvider.SourceName;
            BtpStore.Save(data);

            var named = LearnBondNames(result.Bonds);
            AppendLog($"OK      {T("Elenco dei BTP: {0} titoli, {1} nel tuo conto con il nome di Borsa Italiana.", result.Bonds.Count, named)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendLog($"{T("ERRORE"),-8}{T("Elenco dei BTP: {0}", ex.Message)}");
        }
    }

    public void Dispose()
    {
        _names.Changed -= OnNamesChanged;
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
