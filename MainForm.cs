using DPOper.Forms;
using DPOper.Models;
using DPOper.Services;

namespace DPOper;

public partial class MainForm : Form
{
    private string? _loadedFilePath;
    private List<Operation>? _operations;
    private List<DailySnapshot>? _snapshots;

    private DailyOverviewForm? _dailyOverviewForm;
    private TransfersForm? _transfersForm;
    private InterestForm? _interestForm;
    private SecuritiesForm? _securitiesForm;
    private SecuritiesForm? _cryptoForm;
    private SecuritiesForm? _derivativesForm;
    private RealizedGainsForm? _realizedGainsForm;
    private ChartForm? _chartForm;
    private UpdateLogForm? _updateLogForm;
    private AssetQuotesForm? _assetQuotesForm;
    private PortfolioTreeForm? _portfolioTreeForm;
    private BtpListForm? _btpListForm;

    public MainForm()
    {
        InitializeComponent();
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        var lastFile = RecentFileStore.GetLastFilePath();
        if (lastFile is not null && File.Exists(lastFile))
        {
            LoadFile(lastFile);
            if (_operations is null)
            {
                PromptAndLoadFile();
            }
            return;
        }

        PromptAndLoadFile();
    }

    private void loadCsvMenuItem_Click(object? sender, EventArgs e) => PromptAndLoadFile();

    private void andamentoGiornalieroMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        OpenChild(ref _dailyOverviewForm, () => new DailyOverviewForm(_snapshots!));
    }

    private void exitMenuItem_Click(object? sender, EventArgs e) => Close();

    private void entrateUsciteMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        OpenChild(ref _transfersForm, () => new TransfersForm(_operations!));
    }

    private void interessiMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        OpenChild(ref _interestForm, () => new InterestForm(_operations!));
    }

    private void titoliMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        OpenChild(ref _securitiesForm, () => new SecuritiesForm(_operations!, "Titoli movimentati", null, LoadQuoteTimelines()));
    }

    private void cryptoMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        OpenChild(ref _cryptoForm, () => new SecuritiesForm(_operations!, "Crypto movimentate", ac => ac == "CRYPTO", LoadQuoteTimelines()));
    }

    private void derivatiMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        OpenChild(ref _derivativesForm, () => new SecuritiesForm(_operations!, "Derivati/Warrant movimentati", ac => ac == "DERIVATIVE", LoadQuoteTimelines()));
    }

    private void plusvalenzeMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        OpenChild(ref _realizedGainsForm, () => new RealizedGainsForm(_operations!));
    }

    private void andamentoMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }
        var minDate = _operations!.Min(o => o.Date);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var maxDate = _operations!.Max(o => o.Date);
        if (today > maxDate)
        {
            maxDate = today;
        }

        OpenChild(ref _chartForm, () => new ChartForm(_snapshots!, _operations!, LoadQuoteTimelines(), minDate, maxDate));
    }

    private static readonly string[] AllSupportedAssetClasses = { "CRYPTO", "STOCK", "FUND", "FX" };

    private async void aggiornaCryptoMenuItem_Click(object? sender, EventArgs e) =>
        await RunUpdateAsync(new[] { "CRYPTO" }, "Crypto");

    private async void aggiornaAzioniFondiMenuItem_Click(object? sender, EventArgs e) =>
        await RunUpdateAsync(new[] { "STOCK", "FUND" }, "Azioni e Fondi");

    private async void aggiornaCambioMenuItem_Click(object? sender, EventArgs e) =>
        await RunUpdateAsync(new[] { "FX" }, "Cambio EUR/USD");

    private async void aggiornaTuttoMenuItem_Click(object? sender, EventArgs e) =>
        await RunUpdateAsync(AllSupportedAssetClasses, "tutti gli asset supportati");

    private async Task RunUpdateAsync(string[] assetClasses, string label)
    {
        if (!RequireOperations())
        {
            return;
        }

        var log = GetOrCreateLogForm();
        log.AppendLine($"--- Avvio aggiornamento: {label} ---");

        foreach (var assetClass in assetClasses.Where(ac => ac != "FX"))
        {
            AssetCatalogStore.SyncFromOperations(_operations!, assetClass);
        }
        if (assetClasses.Contains("FX"))
        {
            AssetCatalogStore.EnsureFxEntry();
        }

        var catalog = AssetCatalogStore.Load();
        var entries = catalog.Where(entry => assetClasses.Contains(entry.AssetClass)).ToList();

        if (entries.Count == 0)
        {
            log.AppendLine("Nessun asset da aggiornare per questa categoria.");
            return;
        }

        log.AppendLine($"{entries.Count} asset da aggiornare (fino a 5 in parallelo)...");

        var okCount = 0;
        var failedCount = 0;

        await AssetUpdateService.RunAsync(entries, result =>
        {
            if (result.Success)
            {
                Interlocked.Increment(ref okCount);
                log.AppendLine($"OK      {result.Entry.Name,-24} ({result.Entry.Symbol}) -> {result.Price:F4} EUR");
            }
            else
            {
                Interlocked.Increment(ref failedCount);
                log.AppendLine($"ERRORE  {result.Entry.Name,-24} ({result.Entry.Symbol}): {result.Message}");
            }
        }, CancellationToken.None);

        log.AppendLine($"--- Completato: {okCount} riusciti, {failedCount} falliti ---");

        RefreshAfterQuoteUpdate();
    }

    private void elencoQuotazioniMenuItem_Click(object? sender, EventArgs e)
    {
        OpenChild(ref _assetQuotesForm, () => new AssetQuotesForm());
    }

    private void elencoBtpMenuItem_Click(object? sender, EventArgs e)
    {
        // I BTP non dipendono dal CSV delle operazioni: la voce è sempre disponibile.
        OpenChild(ref _btpListForm, () => new BtpListForm());
    }

    private void composizioneAdUnaDataMenuItem_Click(object? sender, EventArgs e)
    {
        if (!RequireOperations())
        {
            return;
        }

        var minDate = _operations!.Min(o => o.Date);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var maxDate = _operations!.Max(o => o.Date);
        if (today > maxDate)
        {
            maxDate = today;
        }

        OpenChild(ref _portfolioTreeForm, () => new PortfolioTreeForm(_operations!, minDate, maxDate, LoadQuoteTimelines()));
    }

    private UpdateLogForm GetOrCreateLogForm()
    {
        if (_updateLogForm is null || _updateLogForm.IsDisposed)
        {
            _updateLogForm = new UpdateLogForm { MdiParent = this };
            _updateLogForm.Show();
        }
        _updateLogForm.BringToFront();
        return _updateLogForm;
    }

    private void RefreshAfterQuoteUpdate()
    {
        if (_operations is null)
        {
            return;
        }

        _snapshots = PortfolioCalculator.Compute(_operations, LoadQuoteTimelines());

        if (_dailyOverviewForm is { IsDisposed: false })
        {
            OpenChild(ref _dailyOverviewForm, () => new DailyOverviewForm(_snapshots));
        }

        if (_assetQuotesForm is { IsDisposed: false })
        {
            OpenChild(ref _assetQuotesForm, () => new AssetQuotesForm());
        }
    }

    private static Dictionary<string, List<(DateTime Timestamp, decimal Price)>> LoadQuoteTimelines()
    {
        var catalog = AssetCatalogStore.Load();
        return QuoteHistoryStore.LoadAllAsTimelines(catalog);
    }

    /// <summary>
    /// Chiude l'eventuale istanza precedente della finestra figlia e ne apre una nuova con dati
    /// aggiornati: più semplice e sicuro rispetto a riusare l'istanza esistente per queste viste
    /// di sola lettura, i cui dati possono cambiare (nuovo CSV, quotazioni aggiornate).
    /// </summary>
    private void OpenChild<T>(ref T? field, Func<T> factory) where T : Form
    {
        field?.Close();

        var form = factory();
        form.MdiParent = this;
        form.Show();
        form.BringToFront();
        field = form;
    }

    private bool RequireOperations()
    {
        if (_operations is not null)
        {
            return true;
        }

        MessageBox.Show(this, "Carica prima un file CSV da File > Carica CSV operazioni...", "DPOper",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private void PromptAndLoadFile()
    {
        using var dialog = new OpenFileDialog
        {
            DefaultExt = "csv",
            Filter = "File CSV (*.csv)|*.csv|Tutti i file (*.*)|*.*",
            Title = "Seleziona il file CSV delle operazioni",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            if (_loadedFilePath is null)
            {
                statusLabel.Text = "Nessun file caricato. Usa File > Carica CSV operazioni... per iniziare.";
            }
            return;
        }

        LoadFile(dialog.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            Cursor = Cursors.WaitCursor;

            var operations = CsvOperationReader.Read(path);
            if (operations.Count == 0)
            {
                MessageBox.Show(this, "Il file non contiene operazioni.", "DPOper",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var snapshots = PortfolioCalculator.Compute(operations, LoadQuoteTimelines());

            _operations = operations;
            _snapshots = snapshots;
            _loadedFilePath = path;
            RecentFileStore.SetLastFilePath(path);

            var first = snapshots.Min(s => s.Date);
            var last = snapshots.Max(s => s.Date);
            statusLabel.Text = $"{Path.GetFileName(path)} — {operations.Count} operazioni dal {first:dd/MM/yyyy} al {last:dd/MM/yyyy}";
            Text = $"DPOper - {Path.GetFileName(path)}";

            OpenChild(ref _dailyOverviewForm, () => new DailyOverviewForm(_snapshots));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Impossibile leggere il file:\n{ex.Message}", "DPOper",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }
}
