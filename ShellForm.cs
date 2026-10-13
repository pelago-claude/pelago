using Pelago.Services;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.WinForms;

namespace Pelago;

/// <summary>
/// Finestra della nuova interfaccia: un'unica finestra che ospita l'interfaccia web (Blazor) a pagine,
/// con menu laterale e grafici ECharts. I calcoli sono gli stessi servizi C# di sempre.
/// </summary>
public sealed class ShellForm : Form
{
    public ShellForm()
    {
        var testMode = Environment.GetEnvironmentVariable("PELAGO_TEST") == "1";
        var debugPort = Environment.GetEnvironmentVariable("PELAGO_CDP_PORT");

        // La lingua scelta (o quella di Windows) vale da subito, anche per i testi di questa finestra.
        var prefs = new UiPrefs();
        Loc.Init(prefs);

        // Interfaccia del browser incorporato (calendario, menu) nella lingua dell'app: cambia al prossimo avvio se la lingua viene cambiata.
        var browserArgs = $"--lang={Loc.Culture.Name}";
        if (!string.IsNullOrWhiteSpace(debugPort))
        {
            // Solo per le verifiche automatiche: permette di fotografare il contenuto della finestra dell'app.
            browserArgs += $" --remote-debugging-port={debugPort}";
        }
        Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", browserArgs);

        Text = $"Pelago {ChangeLog.Current.Version}" + (DemoAccount.IsActive ? " — " + Loc.T("conto di esempio") : "");
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? Icon;
        Font = new Font("Segoe UI", 9f);
        MinimumSize = new Size(960, 620);

        if (testMode)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-2400, 40);
            ClientSize = new Size(1500, 950);
            ShowInTaskbar = false;
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1400, 860);
            WindowState = FormWindowState.Maximized;
        }

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
        services.AddSingleton<InstrumentNames>();
        services.AddSingleton<IssuerNames>();
        services.AddSingleton<NameEditor>();
        services.AddSingleton<AppState>();
        services.AddSingleton<NativeDialogs>();
        services.AddSingleton(prefs);
        services.AddSingleton<AssistState>();
        var exporter = new PageExporter();
        services.AddSingleton(exporter);

        var webView = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = WebAssets.ExtractAndGetHostPage(),
            Services = services.BuildServiceProvider(),
        };
        webView.WebView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(
                DataLocations.Root, "webview2"),
        };
        webView.RootComponents.Add<App>("#app");
        Controls.Add(webView);

        // Stampa in PDF la pagina visibile (usata dal pulsante «Esporta PDF»)
        exporter.Printer = async path =>
        {
            var core = webView.WebView.CoreWebView2;
            if (core is null)
            {
                return false;
            }
            var settings = core.Environment.CreatePrintSettings();
            settings.Orientation = Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Landscape;
            settings.ShouldPrintBackgrounds = true;
            settings.MarginTop = settings.MarginBottom = 0.4;
            settings.MarginLeft = settings.MarginRight = 0.4;
            return await core.PrintToPdfAsync(path, settings);
        };
    }
}
