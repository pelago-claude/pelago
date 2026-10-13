<#
Controllo automatico dell'interfaccia di Pelago: apre ogni pagina in una finestra fuori schermo (modalità prova, porta di debug) e verifica
  1. che nessuna tabella con righe abbia altezza zero (il difetto che nascondeva la tabella delle posizioni chiuse),
  2. che nessun ISIN sia scritto in chiaro: ogni ISIN deve essere un codice cliccabile (a.isin),
  3. che nessuna pagina mostri il riquadro di errore di Blazor,
  4. che nessun nome di campo (per esempio «_from») sia finito sullo schermo al posto del valore.
Esce con codice 1 se trova problemi.

USO (sempre su una COPIA dei dati, mai su quelli veri):
  .\tools\check-pages.ps1 -Data C:\percorso\copia-dei-dati
Si ferma solo l'istanza che avvia lui: le finestre di Pelago che hai già aperte non vengono chiuse.
#>
param(
    [Parameter(Mandatory)][string]$Data,
    [string]$Exe = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0-windows\pelago.exe'),
    [int]$Port = 9533,
    [int]$Wait = 2200,
    [string]$Lang = 'it',
    [string[]]$Routes = @('', 'daily', 'returns', 'charts', 'heatmap', 'asof', 'transfers', 'interest', 'securities', 'gains', 'closed', 'stats', 'tax', 'costs',
        'quotes', 'currencies', 'coupons', 'btp', 'checks', 'files', 'names', 'history', 'credits')
)

$ErrorActionPreference = 'Stop'
$real = Join-Path $env:LOCALAPPDATA 'Pelago'
if ((Resolve-Path -LiteralPath $Data).Path.TrimEnd('\') -ieq $real.TrimEnd('\')) { throw 'Usa una copia dei dati, non la cartella vera.' }
if (-not (Test-Path -LiteralPath $Exe)) { throw "Non trovo $Exe (compila prima il progetto)." }

# lingua e tema chiaro nella copia
$prefsPath = Join-Path $Data 'webprefs.json'
$prefs = @{}
if (Test-Path -LiteralPath $prefsPath) { (Get-Content -LiteralPath $prefsPath -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $prefs[$_.Name] = $_.Value } }
$prefs['lang'] = $Lang; $prefs['theme'] = 'light'
($prefs | ConvertTo-Json) | Set-Content -LiteralPath $prefsPath -Encoding UTF8

$env:PELAGO_TEST = '1'; $env:PELAGO_CDP_PORT = "$Port"; $env:PELAGO_DATA = $Data
$proc = Start-Process $Exe -PassThru
$problems = New-Object System.Collections.Generic.List[string]
try {
    $targets = $null
    for ($i = 0; $i -lt 40; $i++) { Start-Sleep -Milliseconds 500; try { $targets = Invoke-RestMethod "http://127.0.0.1:$Port/json" -TimeoutSec 2; if ($targets | Where-Object { $_.type -eq 'page' }) { break } } catch { } }
    $page = $targets | Where-Object { $_.type -eq 'page' } | Select-Object -First 1
    if (-not $page) { throw 'nessuna pagina del browser incorporato trovata' }
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    $script:id = 0
    function Send($method, $params = @{}) {
        $script:id++
        $msg = @{ id = $script:id; method = $method; params = $params } | ConvertTo-Json -Depth 10 -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($msg)
        $ws.SendAsync([ArraySegment[byte]]::new($bytes), 'Text', $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        $buf = New-Object byte[] 65536
        while ($true) {
            $sb = New-Object Text.StringBuilder
            do { $r = $ws.ReceiveAsync([ArraySegment[byte]]::new($buf), [Threading.CancellationToken]::None).GetAwaiter().GetResult(); [void]$sb.Append([Text.Encoding]::UTF8.GetString($buf, 0, $r.Count)) } while (-not $r.EndOfMessage)
            $o = $sb.ToString() | ConvertFrom-Json
            if ($o.id -eq $script:id) { return $o }
        }
    }
    [void](Send 'Page.enable')
    [void](Send 'Emulation.setDeviceMetricsOverride' @{ width = 1500; height = 900; deviceScaleFactor = 1; mobile = $false })
    Start-Sleep -Seconds 4

    $check = @'
(() => {
  const re = /\b[A-Z]{2}[A-Z0-9]{9}\d\b/g;
  const raw = [];
  const walker = document.createTreeWalker(document.querySelector("main.content"), NodeFilter.SHOW_TEXT);
  let n;
  while (n = walker.nextNode()) { const m = n.nodeValue.match(re); if (m && !n.parentElement.closest("a.isin")) raw.push(m[0]); }
  const collapsed = [...document.querySelectorAll(".table-wrap")].map(w => ({ h: Math.round(w.getBoundingClientRect().height), rows: w.querySelectorAll("tbody tr").length })).filter(t => t.rows > 0 && t.h < 40).length;
  const errors = document.querySelectorAll(".blazor-error-boundary").length + (getComputedStyle(document.getElementById("blazor-error-ui") || document.body).display === "block" && document.getElementById("blazor-error-ui") ? 1 : 0);
  // nomi di campo C# finiti sullo schermo (per esempio «_from» al posto del valore): un elemento senza figli il cui testo è _qualcosa
  const fields = [...document.querySelectorAll("main.content *")].filter(e => e.children.length === 0 && /^_[A-Za-z]\w*$/.test((e.textContent || "").trim())).map(e => e.textContent.trim());
  return JSON.stringify({ raw: raw.slice(0, 3), collapsed, errors, fields: fields.slice(0, 3), title: (document.querySelector("h1") || {}).textContent || "" });
})()
'@
    foreach ($route in $Routes) {
        [void](Send 'Runtime.evaluate' @{ expression = "Blazor.navigateTo('$route')" })
        Start-Sleep -Milliseconds $Wait
        $res = Send 'Runtime.evaluate' @{ expression = $check; returnByValue = $true }
        $v = $res.result.result.value | ConvertFrom-Json
        $label = if ($route -eq '') { '(panoramica)' } else { $route }
        $status = 'ok'
        if ($v.collapsed -gt 0) { $problems.Add("$label : tabella con righe ma altezza zero"); $status = 'TABELLA NASCOSTA' }
        if (@($v.raw).Count -gt 0) { $problems.Add("$label : ISIN non cliccabile ($($v.raw -join ', '))"); $status = 'ISIN NON CLICCABILE' }
        if ($v.errors -gt 0) { $problems.Add("$label : errore di Blazor nella pagina"); $status = 'ERRORE' }
        if (@($v.fields).Count -gt 0) { $problems.Add("$label : nome di campo visibile al posto del valore ($($v.fields -join ', '))"); $status = 'NOME DI CAMPO VISIBILE' }
        '{0,-14} {1,-22} {2}' -f $label, $v.title.Trim(), $status
    }
    $ws.Dispose()
}
finally {
    if ($proc -and -not $proc.HasExited) { $proc.Kill() }
    Remove-Item Env:\PELAGO_DATA, Env:\PELAGO_TEST, Env:\PELAGO_CDP_PORT -ErrorAction SilentlyContinue
}

if ($problems.Count -gt 0) { ''; $problems | ForEach-Object { "PROBLEMA: $_" }; exit 1 }
''; 'Tutte le pagine sono a posto.'
