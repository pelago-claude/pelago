using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Pelago;
using Pelago.Services;

// Demo nel browser: il conto di esempio (dati inventati) vive in un disco virtuale in memoria, che sparisce chiudendo la pagina.
// Niente rete (le richieste di prezzi sono spente come nel conto di esempio), niente file dell'utente.
Environment.SetEnvironmentVariable(DemoAccount.EnvironmentVariable, "1");
Environment.SetEnvironmentVariable("PELAGO_DATA", "/pelago-demo");

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// stessi servizi del programma per Windows (vedi ShellForm.cs); le preferenze si creano dopo aver preparato i dati del conto di esempio
builder.Services.AddSingleton<InstrumentNames>();
builder.Services.AddSingleton<IssuerNames>();
builder.Services.AddSingleton<NameEditor>();
builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton<NativeDialogs>();
builder.Services.AddSingleton<AssistState>();
builder.Services.AddSingleton<PageExporter>();
builder.Services.AddSingleton(_ =>
{
    var prefs = new UiPrefs();
    Loc.Init(prefs);
    return prefs;
});

var host = builder.Build();

// lingua e tema arrivano dall'indirizzo (?lang=de&theme=dark), cosi' la pagina del sito apre la demo nella sua lingua
var js = (IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>();
var query = js.Invoke<string>("pelagoQuery");
string? Param(string name) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
    .Select(p => p.Split('=', 2)).Where(p => p.Length == 2 && p[0] == name).Select(p => Uri.UnescapeDataString(p[1])).FirstOrDefault();
var lang = Param("lang");
var theme = Param("theme");
if (lang is not ("it" or "en" or "fr" or "de" or "es")) { lang = null; }
if (theme is not ("light" or "dark")) { theme = js.Invoke<string>("pelagoTheme"); }     // senza indicazioni: quello del computer

DemoAccount.Prepare(DataLocations.Root, lang ?? js.Invoke<string>("pelagoLanguage"), theme, DateTime.UtcNow);
_ = host.Services.GetRequiredService<UiPrefs>();     // crea le preferenze e fissa la lingua prima del primo disegno

await host.RunAsync();
