<#
Anteprima del sito vetrina sul tuo PC, prima di pubblicarlo.

Avvia un piccolo server SOLO LOCALE (raggiungibile solo da questo computer) che mostra la cartella site/ con lo stesso percorso
che avra' su GitHub Pages (/pelago/), supporta il riavvolgimento dei video e apre il browser. Non pubblica nulla e non
contatta nessuno.

USO:   .\tools\preview-site.ps1                      apre http://localhost:8080/pelago/
       .\tools\preview-site.ps1 -Port 9000 -NoOpen   un'altra porta, senza aprire il browser

Si ferma con Ctrl+C. Dopo aver cambiato testi o stile: .\tools\build-site.ps1 e poi ricarica la pagina (non serve riavviare).
#>
param(
    [int]$Port = 8080,
    [string]$Prefix = '/pelago/',
    [string]$Root = (Join-Path $PSScriptRoot '..\site'),
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
if (-not (Test-Path (Join-Path $Root 'index.html'))) { throw "Non trovo $Root\index.html: lancia prima .\tools\build-site.ps1" }
if (-not $Prefix.StartsWith('/')) { $Prefix = '/' + $Prefix }
if (-not $Prefix.EndsWith('/')) { $Prefix += '/' }

$mime = @{ '.html' = 'text/html; charset=utf-8'; '.css' = 'text/css; charset=utf-8'; '.js' = 'text/javascript'; '.svg' = 'image/svg+xml'; '.png' = 'image/png'
    '.jpg' = 'image/jpeg'; '.webp' = 'image/webp'; '.mp4' = 'video/mp4'; '.woff2' = 'font/woff2'; '.txt' = 'text/plain; charset=utf-8'; '.xml' = 'application/xml' }

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
$url = "http://localhost:$Port$Prefix"
"Anteprima del sito: $url"
"(solo su questo computer; Ctrl+C per fermare)"
if (-not $NoOpen) { Start-Process $url }

try {
    while ($listener.IsListening) {
        $pending = $listener.GetContextAsync()
        while (-not $pending.AsyncWaitHandle.WaitOne(300)) { }      # attesa a scatti: cosi' Ctrl+C funziona
        $ctx = $pending.GetAwaiter().GetResult()
        $req = $ctx.Request; $res = $ctx.Response
        try {
            $path = [Uri]::UnescapeDataString($req.Url.AbsolutePath)
            if ($path -eq '/') { $res.StatusCode = 302; $res.RedirectLocation = $Prefix; continue }
            if ($path -eq $Prefix.TrimEnd('/')) { $res.StatusCode = 301; $res.RedirectLocation = $Prefix; continue }
            $file = $null
            if ($path.StartsWith($Prefix)) {
                $rel = $path.Substring($Prefix.Length) -replace '/', '\'
                $candidate = [IO.Path]::GetFullPath((Join-Path $Root $rel))
                if ($candidate.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) {      # niente uscite dalla cartella del sito
                    if (Test-Path $candidate -PathType Container) {
                        if (-not $path.EndsWith('/')) { $res.StatusCode = 301; $res.RedirectLocation = $path + '/'; continue }
                        $candidate = Join-Path $candidate 'index.html'
                    }
                    if (Test-Path $candidate -PathType Leaf) { $file = $candidate }
                }
            }
            if ($file) {
                $bytes = [IO.File]::ReadAllBytes($file)
                $ext = [IO.Path]::GetExtension($file).ToLowerInvariant()
                $res.ContentType = $(if ($mime.ContainsKey($ext)) { $mime[$ext] } else { 'application/octet-stream' })
                $res.AddHeader('Accept-Ranges', 'bytes')
                $res.AddHeader('Cache-Control', 'no-store')
                $start = 0; $end = $bytes.Length - 1
                $range = $req.Headers['Range']
                if ($range -match '^bytes=(\d*)-(\d*)$') {
                    if ($Matches[1] -ne '') { $start = [int]$Matches[1] }
                    if ($Matches[2] -ne '') { $end = [Math]::Min([int]$Matches[2], $bytes.Length - 1) }
                    if ($Matches[1] -eq '' -and $Matches[2] -ne '') { $start = [Math]::Max(0, $bytes.Length - [int]$Matches[2]); $end = $bytes.Length - 1 }
                    $res.StatusCode = 206
                    $res.AddHeader('Content-Range', "bytes $start-$end/$($bytes.Length)")
                }
                $len = $end - $start + 1
                $res.ContentLength64 = $len
                $res.OutputStream.Write($bytes, $start, $len)
            }
            else {
                $res.StatusCode = 404
                $nf = Join-Path $Root '404.html'
                if (Test-Path $nf) { $b = [IO.File]::ReadAllBytes($nf); $res.ContentType = 'text/html; charset=utf-8'; $res.OutputStream.Write($b, 0, $b.Length) }
            }
        }
        catch { }
        finally { try { $res.Close() } catch { } }
    }
}
finally { $listener.Stop(); $listener.Close() }
