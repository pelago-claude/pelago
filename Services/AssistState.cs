namespace Pelago.Services;

/// <summary>Un messaggio della chat dell'assistente: la domanda dell'utente oppure una risposta (con eventuali argomenti suggeriti).</summary>
public sealed record ChatMessage(bool FromUser, string Text, string? EntryKey = null, IReadOnlyList<string>? SuggestedKeys = null);

/// <summary>
/// Stato della modalità assistita: se è accesa, cosa sta guardando l'utente (mouse), cosa ha fissato, la chat.
/// Le risposte vengono dai testi di <see cref="HelpTexts"/> (nessun dato lascia il PC).
/// </summary>
public sealed class AssistState
{
    private readonly UiPrefs _prefs;
    private readonly AppState _app;

    public AssistState(UiPrefs prefs, AppState app)
    {
        _prefs = prefs;
        _app = app;
        Enabled = prefs.Get("assist.enabled") == "1";
        Open = prefs.Get("assist.open") != "0";
    }

    public bool Enabled { get; private set; }

    /// <summary>Pannello aperto (true) o ridotto a pulsante (false).</summary>
    public bool Open { get; private set; }

    public string? PageKey { get; private set; }
    public string? HoverKey { get; private set; }
    public string? PinnedKey { get; private set; }
    public List<ChatMessage> Messages { get; } = new();

    public event Action? Changed;

    /// <summary>Cosa mostrare nella scheda «Stai guardando»: fissato, altrimenti sotto il mouse, altrimenti la pagina.</summary>
    public string? ActiveKey => PinnedKey ?? HoverKey ?? PageKey;

    public void SetEnabled(bool value)
    {
        Enabled = value;
        if (value)
        {
            Open = true;
        }
        _prefs.Set("assist.enabled", value ? "1" : "0");
        Changed?.Invoke();
    }

    public void Toggle() => SetEnabled(!Enabled);

    public void SetOpen(bool value)
    {
        Open = value;
        _prefs.Set("assist.open", value ? "1" : "0");
        Changed?.Invoke();
    }

    /// <summary>Chiamato quando si cambia pagina: la scheda torna all'introduzione della pagina.</summary>
    public void SetPage(string relativePath)
    {
        var path = "/" + relativePath.Split('?', '#')[0].Trim('/');

        // un indirizzo con parametro nel percorso (esempio /bonds/DE) usa il testo della pagina madre (/bonds)
        while (!HelpTexts.Has("page:" + path) && path.LastIndexOf('/') > 0)
        {
            path = path[..path.LastIndexOf('/')];
        }

        PageKey = HelpTexts.Has("page:" + path) ? "page:" + path : null;
        HoverKey = null;
        Changed?.Invoke();
    }

    public void Hover(string key)
    {
        if (!HelpTexts.Has(key) || key == HoverKey)
        {
            return;
        }
        HoverKey = key;
        if (PinnedKey is null)
        {
            Changed?.Invoke();
        }
    }

    public void TogglePin()
    {
        PinnedKey = PinnedKey is null ? (HoverKey ?? PageKey) : null;
        Changed?.Invoke();
    }

    public void ShowEntry(string key)
    {
        if (!HelpTexts.Has(key))
        {
            return;
        }
        Messages.Add(new ChatMessage(false, "", key));
        Changed?.Invoke();
    }

    public void ClearChat()
    {
        Messages.Clear();
        Changed?.Invoke();
    }

    /// <summary>Risponde a una domanda cercando nei testi di aiuto.</summary>
    public void Ask(string question)
    {
        question = question.Trim();
        if (question.Length == 0)
        {
            return;
        }

        Messages.Add(new ChatMessage(true, question));

        // «Cosa significa questo?»: nessuna parola significativa → spiego quello che sta guardando.
        if (HelpTexts.Tokens(question).Count == 0)
        {
            Messages.Add(ActiveKey is { } active
                ? new ChatMessage(false, "", active)
                : new ChatMessage(false, Loc.T("Passa il mouse su una voce oppure scrivimi il nome di quello che vuoi capire.")));
            Changed?.Invoke();
            return;
        }

        var results = HelpTexts.Search(question);
        if (results.Count == 0)
        {
            Messages.Add(new ChatMessage(false,
                Loc.T("Non ho trovato nulla su questo. Prova con il nome di una colonna o di una voce (per esempio «rateo», «versamenti netti», «plus/minus»), " +
                "oppure passa il mouse sopra: ti spiego quello che stai guardando.")));
        }
        else
        {
            var best = results[0];
            var others = results.Skip(1).Where(r => r.Score * 2 >= best.Score).Take(3).Select(r => r.Entry.Key).ToList();
            Messages.Add(new ChatMessage(false, "", best.Entry.Key, others.Count > 0 ? others : null));
        }
        Changed?.Invoke();
    }

    /// <summary>Riempie i segnaposto del testo ({total}, {cash}, …) con i valori reali del conto.</summary>
    public string Resolve(string text)
    {
        if (!text.Contains('{'))
        {
            return text;
        }

        var latest = _app.Latest;
        string Money(decimal? v) => v is { } x ? Fmt.Money(x) : "—";

        var values = new Dictionary<string, string>
        {
            ["total"] = Money(latest?.Total),
            ["cash"] = Money(latest?.Cash),
            ["securities"] = Money(latest?.Securities),
            ["bonds"] = Money(latest?.Bonds),
            ["crypto"] = Money(latest?.Crypto),
            ["contrib"] = Money(latest?.Contributions),
            ["gap"] = Money(latest?.Gap),
            ["gappct"] = latest is { Contributions: not 0m } l ? Fmt.Pct(l.Gap / l.Contributions, 1) : "—",
            ["interest"] = _app.Operations is null
                ? "—"
                : Fmt.Money(_app.Operations.Where(o => o.Type == "INTEREST_PAYMENT").Sum(o => o.NetCashImpact)),
            ["realized"] = _app.Operations is null
                ? "—"
                : Fmt.Money(RealizedGainsCalculator.Compute(_app.Operations).Sum(e => e.Gain)),
            ["filename"] = _app.LoadedFilePath is { } p ? Path.GetFileName(p) : "nessun file caricato",
            ["datafolder"] = DataLocations.Root,
        };

        foreach (var (key, value) in values)
        {
            text = text.Replace("{" + key + "}", value);
        }
        return text;
    }
}
