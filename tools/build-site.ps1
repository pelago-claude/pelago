<#
Genera il sito vetrina (cartella site/) dai sorgenti in site-src/ e lo controlla.

  site-src/template.html     la pagina, con segnaposto {{chiave}}
  site-src/strings/*.json    i testi, uno per lingua (it, en, fr, de, es): le stesse chiavi in tutte
  site-src/style.css         lo stile (copiato in site/assets)

Non genera schermate, video e immagini social: quelle stanno gia' in site/assets (le producono make-site-assets.ps1 e record-clip.ps1 in Presentazione\strumenti,
solo sul PC di chi cura il sito). Qui si controlla soltanto che ci siano.

USO:   .\tools\build-site.ps1                      (indirizzo di GitHub Pages)
       .\tools\build-site.ps1 -BaseUrl https://esempio.it/    (se un giorno il sito avra' un dominio proprio)

Esce con codice 1 se trova problemi: segnaposto rimasti, chiavi mancanti in una lingua, file o link locali inesistenti,
risorse caricate da altri siti.
#>
param(
    [string]$BaseUrl = 'https://pelago-claude.github.io/pelago/',
    [string]$Out = (Join-Path $PSScriptRoot '..\site')
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$src = Join-Path $repo 'site-src'
$Out = [IO.Path]::GetFullPath($Out)
if (-not $BaseUrl.EndsWith('/')) { $BaseUrl += '/' }
$utf8 = New-Object System.Text.UTF8Encoding $false

function Write-Text([string]$path, [string]$text) {
    New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
    [IO.File]::WriteAllText($path, $text, $utf8)
}
function Read-Text([string]$path) { [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) }

# ---- lingue: nome nella lingua stessa, bandiera (file di flag-icons), locale per Open Graph
$languages = @(
    @{ Code = 'it'; Name = 'Italiano'; Flag = 'it'; Locale = 'it_IT' },
    @{ Code = 'en'; Name = 'English';  Flag = 'gb'; Locale = 'en_US' },
    @{ Code = 'fr'; Name = "Fran$([char]0xE7)ais"; Flag = 'fr'; Locale = 'fr_FR' },   # codice carattere: PowerShell 5.1 legge male l'UTF-8 senza BOM
    @{ Code = 'de'; Name = 'Deutsch';  Flag = 'de'; Locale = 'de_DE' },
    @{ Code = 'es'; Name = "Espa$([char]0xF1)ol"; Flag = 'es'; Locale = 'es_ES' }
)
$repoUrl = 'https://github.com/pelago-claude/pelago'
$common = @{
    repo      = $repoUrl
    releases  = "$repoUrl/releases/latest"
    issues    = "$repoUrl/issues/new/choose"
    email     = 'pelago_claude@gmail.com'
    samplecsv = "$repoUrl/releases/latest/download/esempio-operazioni.csv"
}
$readme = @{
    it = @{ privacy = "$repoUrl#privacy-cosa-esce-dal-tuo-pc"; build = "$repoUrl#compilare-dal-sorgente"; limits = "$repoUrl#limiti-noti-dello-zainetto-fiscale"; badge = '' }
    en = @{ privacy = "$repoUrl/blob/main/README.en.md#privacy-what-leaves-your-pc"; build = "$repoUrl/blob/main/README.en.md#building-from-source"; limits = "$repoUrl/blob/main/README.en.md#known-limits-of-the-tax-backpack"; badge = '' }
}
foreach ($l in 'fr', 'de', 'es') {
    $readme[$l] = @{ privacy = $readme.en.privacy; build = $readme.en.build; limits = $readme.en.limits; badge = ' <span class="lang-badge" title="English">EN</span>' }
}
$cardIds = 'map', 'time', 'overview', 'charts', 'closed', 'tax', 'bonds', 'assist', 'theme'

# ---- testi: stesse chiavi in tutte le lingue
$strings = @{}
foreach ($l in $languages) {
    $obj = (Read-Text (Join-Path $src "strings\$($l.Code).json")) | ConvertFrom-Json
    $d = @{}
    foreach ($p in $obj.PSObject.Properties) { $d[$p.Name] = [string]$p.Value }
    $strings[$l.Code] = $d
}
$problems = New-Object System.Collections.Generic.List[string]
foreach ($l in $languages | Where-Object { $_.Code -ne 'it' }) {
    foreach ($k in $strings.it.Keys) { if (-not $strings[$l.Code].ContainsKey($k)) { $problems.Add("manca la chiave '$k' in $($l.Code).json") } }
    foreach ($k in $strings[$l.Code].Keys) { if (-not $strings.it.ContainsKey($k)) { $problems.Add("chiave '$k' di $($l.Code).json assente in it.json") } }
}

$template = Read-Text (Join-Path $src 'template.html')

function Expand-Tokens([string]$text, [hashtable]$values, [string]$where) {
    $state = @{ changed = $false }
    for ($pass = 0; $pass -lt 4; $pass++) {          # piu' passate: certi testi contengono a loro volta dei segnaposto ({{issues}}, {{email}})
        $state.changed = $false
        $text = [regex]::Replace($text, '\{\{([A-Za-z0-9_.]+)\}\}', {
            param($m)
            $key = $m.Groups[1].Value
            if ($values.ContainsKey($key)) { $state.changed = $true; return [string]$values[$key] }
            return $m.Value
        })
        if (-not $state.changed) { break }
    }
    foreach ($m in [regex]::Matches($text, '\{\{([A-Za-z0-9_.]+)\}\}')) { $problems.Add("segnaposto sconosciuto $($m.Value) in $where") }
    return $text
}

# ---- pagine
$redirect = @'
<script>
/* Prima visita alla pagina italiana senza una scelta esplicita: porta alla lingua del browser. Non salva nulla. */
(function () { try {
  if (location.hash || /[?&]lang=/.test(location.search)) return;
  var r = document.referrer; if (r && r.indexOf(location.origin) === 0) return;
  var l = ((navigator.languages && navigator.languages[0]) || navigator.language || 'it').slice(0, 2).toLowerCase();
  var to = { en: 'en/', fr: 'fr/', de: 'de/', es: 'es/' }[l];
  if (to) location.replace(to);
} catch (e) {} })();
</script>

'@

function Page-Url([string]$code) { if ($code -eq 'it') { return $BaseUrl } else { return "$BaseUrl$code/" } }

foreach ($lang in $languages) {
    $code = $lang.Code
    $s = $strings[$code]
    $root = if ($code -eq 'it') { '' } else { '../' }

    $langLinks = foreach ($t in $languages) {
        $href = if ($t.Code -eq $code) { './' } elseif ($code -eq 'it') { "$($t.Code)/" } elseif ($t.Code -eq 'it') { '../' } else { "../$($t.Code)/" }
        $current = if ($t.Code -eq $code) { ' aria-current="page"' } else { '' }
        "      <a href=`"$href`" hreflang=`"$($t.Code)`" lang=`"$($t.Code)`" title=`"$($t.Name)`" aria-label=`"$($t.Name)`"$current><img src=`"${root}assets/img/flags/$($t.Flag).svg`" alt=`"`" width=`"24`" height=`"18`"></a>"
    }
    $hreflangs = (@($languages | ForEach-Object { "<link rel=`"alternate`" hreflang=`"$($_.Code)`" href=`"$(Page-Url $_.Code)`">" }) + "<link rel=`"alternate`" hreflang=`"x-default`" href=`"$BaseUrl`">") -join "`n"

    $cards = foreach ($id in $cardIds) {
        $base = "${root}assets/img/shots/$code/$id"
        @(
            '        <li class="card">',
            "          <a class=`"shot`" href=`"$base-1600.webp`" aria-label=`"$($s['card.zoom']): $($s["card.$id.t"])`"><img src=`"$base-800.webp`" srcset=`"$base-800.webp 800w, $base-1600.webp 1600w`" sizes=`"(min-width: 960px) 340px, (min-width: 620px) 46vw, 92vw`" width=`"1600`" height=`"900`" alt=`"$($s["card.$id.alt"])`" loading=`"lazy`" decoding=`"async`"></a>",
            "          <h3>$($s["card.$id.t"])</h3>",
            "          <p>$($s["card.$id.d"])</p>",
            '        </li>'
        ) -join "`n"
    }

    $jsonLd = [ordered]@{
        '@context'          = 'https://schema.org'
        '@type'             = 'SoftwareApplication'
        name                = 'Pelago'
        applicationCategory = 'FinanceApplication'
        operatingSystem     = 'Windows 10, Windows 11'
        description         = $s['meta.desc']
        inLanguage          = $code
        url                 = (Page-Url $code)
        downloadUrl         = $common.releases
        license             = 'https://www.gnu.org/licenses/gpl-3.0.html'
        isAccessibleForFree = $true
        offers              = [ordered]@{ '@type' = 'Offer'; price = '0'; priceCurrency = 'EUR' }
        author              = [ordered]@{ '@type' = 'Person'; name = 'David' }
        image               = "${BaseUrl}assets/img/og-$code.jpg"
    } | ConvertTo-Json -Depth 5 -Compress

    $values = @{}
    foreach ($k in $s.Keys) { $values[$k] = $s[$k] }
    foreach ($k in $common.Keys) { $values[$k] = $common[$k] }
    $values['lang'] = $code
    $values['root'] = $root
    $values['base'] = $BaseUrl
    $values['canonical'] = (Page-Url $code)
    $values['locale'] = $lang.Locale
    $values['hreflangs'] = $hreflangs
    $values['langlinks'] = $langLinks -join "`n"
    $values['cards'] = $cards -join "`n"
    $values['jsonld'] = $jsonLd
    $values['redirect'] = $(if ($code -eq 'it') { $redirect } else { '' })
    $values['readme_privacy'] = $readme[$code].privacy
    $values['readme_build'] = $readme[$code].build
    $values['readme_limits'] = $readme[$code].limits
    $values['readme_badge'] = $readme[$code].badge

    $html = Expand-Tokens $template $values "pagina $code"
    $target = if ($code -eq 'it') { Join-Path $Out 'index.html' } else { Join-Path $Out "$code\index.html" }
    Write-Text $target $html
}

# ---- file comuni: stile, carattere, bandiere (con le loro licenze), 404, robots, sitemap
New-Item -ItemType Directory -Force (Join-Path $Out 'assets\fonts'), (Join-Path $Out 'assets\img\flags') | Out-Null
Copy-Item (Join-Path $src 'style.css') (Join-Path $Out 'assets\style.css') -Force
Copy-Item (Join-Path $repo 'wwwroot\fonts\InstrumentSans-latin.woff2'), (Join-Path $repo 'wwwroot\fonts\LICENSE.txt') (Join-Path $Out 'assets\fonts') -Force
foreach ($l in $languages) { Copy-Item (Join-Path $repo "wwwroot\img\flags\$($l.Flag).svg") (Join-Path $Out 'assets\img\flags') -Force }
Copy-Item (Join-Path $repo 'wwwroot\img\flags\LICENSE.txt') (Join-Path $Out 'assets\img\flags') -Force
Write-Text (Join-Path $Out '.nojekyll') ''

$notFound = @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex">
<title>Pelago: $($strings.en['nf.title']) / $($strings.it['nf.title'])</title>
<style>
:root { color-scheme: light dark; }
body { font: 17px/1.6 system-ui, "Segoe UI", sans-serif; max-width: 34em; margin: 12vh auto; padding: 0 20px; background: Canvas; color: CanvasText; }
a { color: LinkText; }
</style>
</head>
<body>
<h1>Pelago</h1>
<p>$($strings.en['nf.text']) &mdash; $($strings.it['nf.text'])</p>
<p><a href="$BaseUrl">$($strings.en['nf.back'])</a> &middot; <a href="$BaseUrl">$($strings.it['nf.back'])</a></p>
</body>
</html>
"@
Write-Text (Join-Path $Out '404.html') $notFound

Write-Text (Join-Path $Out 'robots.txt') "User-agent: *`nAllow: /`n`nSitemap: ${BaseUrl}sitemap.xml`n"

$urls = foreach ($l in $languages) {
    $alts = ($languages | ForEach-Object { "    <xhtml:link rel=`"alternate`" hreflang=`"$($_.Code)`" href=`"$(Page-Url $_.Code)`"/>" }) -join "`n"
    "  <url>`n    <loc>$(Page-Url $l.Code)</loc>`n$alts`n    <xhtml:link rel=`"alternate`" hreflang=`"x-default`" href=`"$BaseUrl`"/>`n  </url>"
}
Write-Text (Join-Path $Out 'sitemap.xml') ("<?xml version=`"1.0`" encoding=`"UTF-8`"?>`n<urlset xmlns=`"http://www.sitemaps.org/schemas/sitemap/0.9`" xmlns:xhtml=`"http://www.w3.org/1999/xhtml`">`n" + ($urls -join "`n") + "`n</urlset>`n")

# ---- controlli sul risultato
$allowedLinkHosts = 'github.com', 'pelago-claude.github.io'
$externalHosts = New-Object System.Collections.Generic.HashSet[string]
foreach ($page in Get-ChildItem $Out -Recurse -Filter *.html) {
    $text = Read-Text $page.FullName
    $dir = Split-Path $page.FullName
    $rel = $page.FullName.Substring($Out.Length).TrimStart('\')
    if ($rel -eq '404.html') { continue }

    # risorse caricate (src, stylesheet, preload, srcset, poster): devono esistere e non stare su altri siti
    $refs = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($text, '(?:src|poster)="([^"]+)"')) { $refs.Add($m.Groups[1].Value) }
    foreach ($m in [regex]::Matches($text, '<link[^>]+rel="(?:stylesheet|preload|icon|apple-touch-icon)"[^>]*href="([^"]+)"')) { $refs.Add($m.Groups[1].Value) }
    foreach ($m in [regex]::Matches($text, 'srcset="([^"]+)"')) { foreach ($part in $m.Groups[1].Value.Split(',')) { $refs.Add($part.Trim().Split(' ')[0]) } }
    foreach ($ref in $refs) {
        if ($ref -match '^https?://') { $problems.Add("$rel carica una risorsa da un altro sito: $ref"); continue }
        $file = Join-Path $dir ($ref -replace '/', '\')
        if (-not (Test-Path -LiteralPath $file)) { $problems.Add("$rel : manca il file $ref") }
    }
    # collegamenti: locali devono esistere, esterni solo verso gli host previsti
    foreach ($m in [regex]::Matches($text, '<a [^>]*href="([^"]+)"')) {
        $href = $m.Groups[1].Value
        if ($href.StartsWith('#') -or $href.StartsWith('mailto:')) { continue }
        if ($href -match '^https?://([^/]+)') {
            [void]$externalHosts.Add($Matches[1])
            if ($allowedLinkHosts -notcontains $Matches[1]) { $problems.Add("$rel : collegamento verso un host non previsto: $href") }
            continue
        }
        $path = ($href -split '#')[0] -replace '/', '\'
        if ($path -eq '' -or $path -eq '.\') { continue }
        $file = Join-Path $dir $path
        if (-not (Test-Path -LiteralPath $file)) { $problems.Add("$rel : collegamento locale inesistente $href") }
    }
    foreach ($m in [regex]::Matches($text, 'href="#([^"]+)"')) {
        if ($text -notmatch ('id="' + [regex]::Escape($m.Groups[1].Value) + '"')) { $problems.Add("$rel : l'ancora #$($m.Groups[1].Value) non esiste") }
    }
    foreach ($m in [regex]::Matches($text, '<img [^>]*>')) { if ($m.Value -notmatch ' alt="') { $problems.Add("$rel : immagine senza alt: $($m.Value.Substring(0, [Math]::Min(80, $m.Value.Length)))") } }
    if ($text -match '[\x00-\x08]|\xC3[\x80-\xBF]') { $problems.Add("$rel : caratteri strani (codifica?)") }
    if ($text -match '(?i)[A-Z]:\\Users\\|/Users/|/home/') { $problems.Add("$rel : un percorso di una cartella personale nel testo") }
}
foreach ($l in $languages) {
    foreach ($f in "assets\img\og-$($l.Code).jpg", "assets\video\pelago-$($l.Code).mp4", "assets\img\shots\$($l.Code)\map-1600.webp") {
        if (-not (Test-Path (Join-Path $Out $f))) { $problems.Add("manca site\$f (video: record-clip.ps1; immagini: make-site-assets.ps1; in Presentazione\strumenti)") }
    }
}
foreach ($f in 'assets\img\pelago-32.png', 'assets\img\pelago-64.png', 'assets\img\pelago-180.png') {
    if (-not (Test-Path (Join-Path $Out $f))) { $problems.Add("manca site\$f") }
}

$pageCount = @(Get-ChildItem $Out -Recurse -Filter index.html).Count
"Pagine generate: $pageCount  |  host dei collegamenti esterni: $(($externalHosts | Sort-Object) -join ', ')"
$size = (Get-ChildItem $Out -Recurse -File | Measure-Object Length -Sum).Sum
"Peso del sito: {0:N1} MB" -f ($size / 1MB)
if ($problems.Count -gt 0) { ''; $problems | Sort-Object -Unique | ForEach-Object { "PROBLEMA: $_" }; exit 1 }
'Sito generato e controllato.'
