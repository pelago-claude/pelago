<#
Costruisce la demo nel browser: lo stesso programma (pagine, calcoli, grafici) ospitato in una pagina web, con il conto di esempio
(dati inventati). Non e' un secondo programma da mantenere: i sorgenti sono quelli di Pelago, copiati al volo.

  1. costruisce in Web\_build\ (cartella generata, non si committa) un progetto completo: copia Services, Models, Components, Pages,
     Shared, App.razor, _Imports.razor, le traduzioni, il conto di esempio e wwwroot, TOGLIENDO i pezzi che esistono solo in Windows
     (finestre di dialogo, estrazione dell'interfaccia) e aggiungendo i file di Web\ (Program.cs, NativeDialogs.cs, Pelago.Web.csproj,
     pagina ospite);
  2. lo pubblica (Blazor WebAssembly);
  3. mette il risultato in site\provalo\demo\ (con il percorso giusto per GitHub Pages).

USO:   .\tools\build-demo.ps1
       .\tools\build-demo.ps1 -BasePath /provalo/demo/        (se il sito avra' un dominio proprio)
       .\tools\build-demo.ps1 -NoCopyToSite                    (lascia il risultato in Web\_build\publish)

Serve internet solo la prima volta (pacchetti NuGet di Blazor WebAssembly).
#>
param(
    [string]$BasePath = '/pelago/provalo/demo/',
    [string]$Out = (Join-Path $PSScriptRoot '..\site\provalo\demo'),
    [switch]$NoCopyToSite
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$web = Join-Path $repo 'Web'
$build = Join-Path $web '_build'
$publish = Join-Path $build 'publish'
if (-not $BasePath.StartsWith('/')) { $BasePath = '/' + $BasePath }
if (-not $BasePath.EndsWith('/')) { $BasePath += '/' }

# ---- 1. progetto completo in Web\_build
if (Test-Path $build) { Remove-Item $build -Recurse -Force }
New-Item -ItemType Directory -Force $build | Out-Null

foreach ($d in 'Services', 'Models', 'Components', 'Pages', 'Shared', 'i18n', 'demo') {
    Copy-Item (Join-Path $repo $d) (Join-Path $build $d) -Recurse -Force
}
Copy-Item (Join-Path $repo 'App.razor'), (Join-Path $repo '_Imports.razor') $build -Force
New-Item -ItemType Directory -Force (Join-Path $build 'samples') | Out-Null
Copy-Item (Join-Path $repo 'samples\esempio-operazioni.csv') (Join-Path $build 'samples') -Force

# pezzi che esistono solo in Windows: la demo ha i suoi (Web\NativeDialogs.cs, Web\Program.cs); la pagina "Dati e file" parla di cartelle
# e backup sul PC (usa anche gli appunti di Windows) e nella demo non c'e'
foreach ($f in 'Services\NativeDialogs.cs', 'Services\WebAssets.cs', 'Services\DataGridViewSorter.cs', 'Services\FormLayoutPersistence.cs', 'Pages\Files.razor') {
    $path = Join-Path $build $f
    if (Test-Path $path) { Remove-Item $path }
}
Copy-Item (Join-Path $web 'Pelago.Web.csproj'), (Join-Path $web 'Program.cs'), (Join-Path $web 'NativeDialogs.cs') $build -Force

# interfaccia web (CSS, script, caratteri, bandiere): la stessa del programma; la pagina ospite e' quella della demo
Copy-Item (Join-Path $repo 'wwwroot') (Join-Path $build 'wwwroot') -Recurse -Force
$index = [IO.File]::ReadAllText((Join-Path $web 'index.web.html'), [Text.Encoding]::UTF8).Replace('<base href="/" />', "<base href=`"$BasePath`" />")
[IO.File]::WriteAllText((Join-Path $build 'wwwroot\index.html'), $index, (New-Object Text.UTF8Encoding $false))

# ---- 2. pubblicazione
Push-Location $build
try {
    dotnet publish Pelago.Web.csproj -c Release -o $publish --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish e' fallito ($LASTEXITCODE)" }
}
finally { Pop-Location }

$size = (Get-ChildItem (Join-Path $publish 'wwwroot') -Recurse -File | Measure-Object Length -Sum).Sum
"Demo pubblicata in $publish\wwwroot ({0:N1} MB in tutto, file compressi compresi)" -f ($size / 1MB)

# ---- 3. nel sito
if (-not $NoCopyToSite) {
    $Out = [IO.Path]::GetFullPath($Out)
    if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
    New-Item -ItemType Directory -Force $Out | Out-Null
    Copy-Item (Join-Path $publish 'wwwroot\*') $Out -Recurse -Force
    "Copiata in $Out"
}
