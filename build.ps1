# Baut eine eigenständige EXE (kein installiertes .NET nötig) nach .\publish
param(
    [switch]$FrameworkDependent  # kleinere EXE, benötigt aber die .NET 9 Desktop Runtime
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\OmsiHofManager\OmsiHofManager.csproj"
$output = Join-Path $PSScriptRoot "publish"

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

dotnet publish $project -c Release -r win-x64 -o $output `
    --self-contained $selfContained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=$selfContained `
    -p:DebugType=none

if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen" }
Write-Host "`nFertig: $output\OmsiHofManager.exe" -ForegroundColor Green

# Release-Zip: EXE + Anleitung + Lizenz (Textdateien mit CRLF, damit sie im Editor sauber aussehen)
# Nur für die eigenständige EXE – die Anleitung verspricht "kein .NET nötig".
if ($FrameworkDependent) { return }
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$releaseDir = Join-Path $PSScriptRoot "release"
$staging = Join-Path $releaseDir "OmsiHofManager_v$version"
$zip = "$staging.zip"

Remove-Item $staging, $zip -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item (Join-Path $output "OmsiHofManager.exe") $staging
$utf8Bom = New-Object System.Text.UTF8Encoding $true
foreach ($pair in @(@("release\Anleitung.txt", "Anleitung.txt"), @("LICENSE", "LICENSE.txt"))) {
    $text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $pair[0])) -replace "`r?`n", "`r`n"
    [IO.File]::WriteAllText((Join-Path $staging $pair[1]), $text, $utf8Bom)
}
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $staging -Recurse -Force
Write-Host "Release: $zip" -ForegroundColor Green
